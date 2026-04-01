using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Consumers;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

namespace Minnaloushe.Core.MessageQueues.Abstractions;

/// <summary>
/// Hosted service that manages consumer workers for a specific message type.
/// Runs the initializer once, then starts N parallel consumer workers based on Parallelism setting.
/// </summary>
/// <typeparam name="TMessage">The type of message being consumed.</typeparam>
/// <typeparam name="TClient">The client type used by the consumer.</typeparam>
public sealed class ConsumerHostedService<TMessage, TClient>(
    string consumerName,
    IServiceProvider serviceProvider,
    IOptionsMonitor<MessageQueueOptions> optionsMonitor,
    ILogger<ConsumerHostedService<TMessage, TClient>> logger
) : IHostedService, IAsyncDisposable
    where TClient : class
{
    private readonly List<ConsumerWorker<TMessage, TClient>> _workers = [];
    private CancellationTokenSource? _cts;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogConsumerHostedServiceStarting(consumerName);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var options = optionsMonitor.Get(consumerName);

        // Run initializer first
        await RunInitializerAsync(_cts.Token);

        // Start N parallel consumer workers
        var parallelism = Math.Max(1, options.Parallelism);
        logger.LogStartingConsumerWorkers(consumerName, parallelism);

        for (var i = 0; i < parallelism; i++)
        {
            var worker = CreateWorker(i);
            _workers.Add(worker);
            await worker.StartAsync(_cts.Token);
        }

        logger.LogConsumerHostedServiceStarted(consumerName, parallelism);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogConsumerHostedServiceStopping(consumerName);

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        var stopTasks = _workers.Select(w => w.StopAsync()).ToArray();
        await Task.WhenAll(stopTasks);

        logger.LogConsumerHostedServiceStopped(consumerName);
    }

    private async Task RunInitializerAsync(CancellationToken cancellationToken)
    {
        logger.LogRunningConsumerInitializer(consumerName);

        try
        {
            // Resolve keyed initializer for this consumer
            var initializer = serviceProvider.GetKeyedService<IConsumerInitializer>(consumerName);

            if (initializer is not null)
            {
                await initializer.InitializeAsync(cancellationToken);
                logger.LogConsumerInitializerCompleted(consumerName);
            }
            else
            {
                logger.LogNoConsumerInitializerFound(consumerName);
            }
        }
        catch (Exception ex)
        {
            logger.LogConsumerInitializerFailed(consumerName, ex);
            throw;
        }
    }

    private ConsumerWorker<TMessage, TClient> CreateWorker(int index)
    {
        // Resolve client provider keyed by consumer name (registered as alias to connection)
        var clientProvider = serviceProvider.GetRequiredKeyedService<IClientProvider<TClient>>(consumerName + index);
        var engineFactory = serviceProvider.GetRequiredService<IMessageEngineFactory<TMessage, TClient>>();
        var workerLogger = serviceProvider.GetRequiredService<ILogger<ConsumerWorker<TMessage, TClient>>>();

        // Resolve the error handling strategy factory and create a strategy for this consumer
        var strategyFactory = serviceProvider.GetRequiredKeyedService<IErrorHandlingStrategyFactory>(consumerName);
        var errorHandlingStrategy = strategyFactory.Create(consumerName);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return new ConsumerWorker<TMessage, TClient>(
            consumerName,
            clientProvider,
            optionsMonitor,
            engineFactory,
            errorHandlingStrategy,
            scopeFactory,
            workerLogger);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var worker in _workers)
        {
            await (worker as IAsyncDisposable).DisposeAsync();
        }

        _workers.Clear();
        _cts?.Dispose();
    }
}
