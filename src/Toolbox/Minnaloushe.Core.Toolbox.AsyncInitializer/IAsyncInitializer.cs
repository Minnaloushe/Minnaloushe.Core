namespace Minnaloushe.Core.Toolbox.AsyncInitializer;

/// <summary>
/// Defines a contract for asynchronously initializing resources or performing setup operations.
/// </summary>
/// <remarks>Implementations of this interface should provide logic to initialize resources or perform setup tasks
/// that may require asynchronous execution. The initialization process can be cancelled by passing a cancellation token
/// to the method. This interface is typically used to ensure that components are properly initialized before use,
/// especially in scenarios where initialization may involve I/O-bound or long-running operations.</remarks>
public interface IAsyncInitializer
{
    /// <summary>
    /// Asynchronously initializes the component, performing any required async initialisation logic that can not be performed
    /// in synchronous constructor call.
    /// Can throw an exception if initialisation fails.
    /// IMPORTANT: Can be invoked multiple times until invocation succeeds; implementations must be idempotent.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the initialization operation.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task<bool> InitializeAsync(CancellationToken cancellationToken);
}