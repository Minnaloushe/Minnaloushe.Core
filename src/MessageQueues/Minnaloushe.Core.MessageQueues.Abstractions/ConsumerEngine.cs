using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;

namespace Minnaloushe.Core.MessageQueues.Abstractions;

public abstract class ConsumerEngine<TMessage>(
    string consumerName,
    IConsumer<TMessage> handler,
    IErrorHandlingStrategy errorHandlingStrategy,
    IMessageQueueNamingConventionsProvider namingConventionsProvider,
    MessageQueueOptions options,
    ILogger logger
) : IMessageEngine
{
    private int _inFlight = 0;
    private TaskCompletionSource? _tcs;
    protected string ConsumerName => consumerName;
    protected abstract Task<IMessageContext<TMessage>> ReceiveAsync(
        CancellationToken ct
    );
    protected MessageQueueOptions Options => options;

    public abstract Task StopAsync(CancellationToken ct);

    public abstract Task OnStartAsync(CancellationToken ct);

    protected Task WaitForIdleAsync()
    {
        // Fast-path: already idle
        if (Volatile.Read(ref _inFlight) == 0)
        {
            return Task.CompletedTask;
        }

        _tcs ??= new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        return _tcs.Task;
    }

    public async Task RunAsync(CancellationToken serviceStop, CancellationToken processingStop)
    {
        await OnStartAsync(serviceStop);

        try
        {
            while (!processingStop.IsCancellationRequested)
            {
                var ctx = await ReceiveAsync(processingStop);

                Interlocked.Increment(ref _inFlight);

                bool ok = false;
                Exception? processingException = null;

                try
                {
                    using var loggerScope = logger.BeginScope("Consumer: {ConsumerName}, MessageId: {MessageId}", ConsumerName, ctx.Key);
                    // If message is null there is nothing to process (might be a tombstone), just ack and continue
                    ok = ctx.Message == null || await handler.HandleMessageAsync(
                        new MessageEnvelop<TMessage>(ctx.Message, ctx.Key, ctx.Headers),
                        processingStop
                        );
                }
                catch (Exception ex)
                {
                    processingException = ex;
                    logger.LogError(ex, "Error processing message from {ConsumerName}", ConsumerName);
                }
                finally
                {
                    if (Interlocked.Decrement(ref _inFlight) == 0)
                    {
                        _tcs?.TrySetResult();
                    }
                }

                if (ok && processingException is null)
                {
                    await ctx.AckAsync(serviceStop);
                }
                else
                {
                    // Use error handling strategy
                    var failedDetails = new FailedMessageDetails(
                        ctx.RawMessage,
                        processingException,
                        namingConventionsProvider.GetTopicName<TMessage>(),
                        options.ServiceKey,
                        DateTimeOffset.UtcNow,
                        typeof(TMessage))
                    {
                        OriginalHeaders = ctx.Headers
                    };

                    var result = await errorHandlingStrategy.HandleErrorAsync(failedDetails, serviceStop);

                    // Apply the result based on strategy outcome
                    switch (result)
                    {
                        case ErrorHandlingResult.Requeued:
                            await ctx.NackAsync(true, serviceStop);
                            break;

                        case ErrorHandlingResult.Discarded:
                            await ctx.NackAsync(false, serviceStop);
                            break;
                        case ErrorHandlingResult.SentToDeadLetter:
                        case ErrorHandlingResult.Acknowledged:
                            await ctx.AckAsync(serviceStop);
                            break;
                    }
                }
            }

            await StopAsync(serviceStop);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Main consumer loop failed");
        }
    }
}