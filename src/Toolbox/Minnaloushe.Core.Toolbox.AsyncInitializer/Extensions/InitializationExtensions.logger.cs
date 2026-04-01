using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.Toolbox.AsyncInitializer;

internal static partial class InitializationExtensionsLogger
{
    [LoggerMessage(1000, LogLevel.Debug, "No IAsyncInitializer implementations found; skipping async initialization.")]
    public static partial void LogNoInitializers(this ILogger logger);

    [LoggerMessage(1001, LogLevel.Information, "Async initialization completed in {elapsed}s.")]
    public static partial void LogAsyncInitializationCompleted(this ILogger logger, double elapsed);

    [LoggerMessage(1002, LogLevel.Warning, "Async initialization cancelled by host shutdown.")]
    public static partial void LogAsyncInitializationCancelled(this ILogger logger);

    [LoggerMessage(1003, LogLevel.Error, "Async initialization timed out after {timeout}s.")]
    public static partial void LogAsyncInitializationTimedOut(this ILogger logger, double timeout);

    [LoggerMessage(1004, LogLevel.Error, "Async initialization failed. Host will not start.")]
    public static partial void LogAsyncInitializationFailed(this ILogger logger, Exception ex);

}
