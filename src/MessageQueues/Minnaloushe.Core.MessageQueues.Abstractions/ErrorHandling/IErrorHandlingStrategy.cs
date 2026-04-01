namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Strategy interface for handling message processing errors.
/// Each consumer can have its own error handling strategy.
/// </summary>
public interface IErrorHandlingStrategy
{
    /// <summary>
    /// Handles a failed message according to the strategy.
    /// </summary>
    /// <param name="details">Details about the failed message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the error handling operation.</returns>
    Task<ErrorHandlingResult> HandleErrorAsync(FailedMessageDetails details, CancellationToken cancellationToken);
}