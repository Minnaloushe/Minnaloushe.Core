using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Consumers;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

namespace Minnaloushe.Core.MessageQueues.Abstractions;

public class ConsumerWorker<TMessage, TClient>(
    string name,
    IClientProvider<TClient> clientProvider,
    IOptionsMonitor<MessageQueueOptions> options,
    IMessageEngineFactory<TMessage, TClient> engine,
    IErrorHandlingStrategy errorHandlingStrategy,
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger
    ) : IAsyncDisposable
    where TClient : class

{
    protected MessageQueueOptions Options => options.Get(name);
    private Task? _consumeTask;
    private CancellationTokenSource _internalCts = new();

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_consumeTask is not null)
        {
            logger.LogConsumerAlreadyStarted();
            throw new InvalidOperationException("Consumer has already been started.");
        }

        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumeTask = Task.Run(() => MainOuterLoopAsync(_internalCts.Token), CancellationToken.None);
        logger.LogConsumerStarted();

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_consumeTask is null)
        {
            return;
        }

        logger.LogConsumerStoppingRequested();

        await _internalCts.CancelAsync();

        try
        {
            await _consumeTask;
            logger.LogConsumerStopped();
        }
        catch (OperationCanceledException)
        {
            logger.LogConsumerStoppedByCancellation();
        }
    }

    private async Task MainOuterLoopAsync(CancellationToken cancellationToken)
    {
        logger.LogConsumerLoopStarted();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();

                using var lease = clientProvider.Acquire();
                var client = lease.Client;

                var consumer = scope.ServiceProvider.GetRequiredService<IConsumer<TMessage>>();

                logger.LogClientAcquired();
                var impl = engine.CreateEngine(
                    name,
                    client,
                    consumer,
                    Options,
                    errorHandlingStrategy);


                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    lease.CancellationToken);

                await impl.RunAsync(cancellationToken, linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogConsumerLoopCancelled();
                break;
            }
            catch (Exception ex)
            {
                logger.LogConsumerLoopError(ex);

                // Wait before retrying to avoid tight error loops
                try
                {
                    await Task.Delay(Options.ConsumerErrorDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            finally
            {
                logger.LogClientReleased();
            }
        }

        logger.LogConsumerLoopCompleted();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_consumeTask != null)
        {
            await CastAndDispose(_consumeTask);
        }

        await CastAndDispose(_internalCts);

        GC.SuppressFinalize(this);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
            {
                await resourceAsyncDisposable.DisposeAsync();
            }
            else
            {
                resource.Dispose();
            }
        }
    }
}

public interface IConsumerInitializer
{
    Task InitializeAsync(CancellationToken ct);
}