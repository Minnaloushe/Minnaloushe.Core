using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Base implementation of error handling strategy factory.
/// Creates the appropriate strategy based on consumer options.
/// </summary>
public class ErrorHandlingStrategyFactory(
    IOptionsMonitor<MessageQueueOptions> optionsMonitor,
    ILoggerFactory loggerFactory
) : IErrorHandlingStrategyFactory
{
    public virtual IErrorHandlingStrategy Create(string consumerName)
    {
        var options = optionsMonitor.Get(consumerName);
        var logger = loggerFactory.CreateLogger<ErrorHandlingStrategy>();

        return options.ErrorHandling switch
        {
            ErrorHandlingStrategy.NackAndRequeue =>
                new NackAndRequeueStrategy(logger),

            ErrorHandlingStrategy.NackAndDiscard =>
                new NackAndDiscardStrategy(logger),

            ErrorHandlingStrategy.Ack =>
                new AckOnErrorStrategy(logger),

            ErrorHandlingStrategy.DeadLetter =>
                CreateDeadLetterStrategy(consumerName, options, logger),

            _ => new NackAndDiscardStrategy(logger)
        };
    }

    protected virtual IErrorHandlingStrategy CreateDeadLetterStrategy(
        string consumerName,
        MessageQueueOptions options,
        ILogger logger)
    {
        // This should be overridden by provider-specific factories
        // Default implementation logs a warning and falls back to discard
        logger.LogWarning(
            "Dead letter strategy requested but no provider-specific implementation available. Falling back to NackAndDiscard.");
        return new NackAndDiscardStrategy(logger);
    }
}