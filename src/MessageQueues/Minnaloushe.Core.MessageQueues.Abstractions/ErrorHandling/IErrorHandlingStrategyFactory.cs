namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Factory for creating error handling strategies based on consumer options.
/// </summary>
public interface IErrorHandlingStrategyFactory
{
    /// <summary>
    /// Creates an error handling strategy for the specified consumer.
    /// </summary>
    /// <param name="consumerName">The name of the consumer.</param>
    /// <returns>The error handling strategy.</returns>
    IErrorHandlingStrategy Create(string consumerName);
}