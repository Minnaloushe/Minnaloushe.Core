using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.Toolbox.AsyncInitializer;

internal static partial class AsyncInitializerServiceLogger
{
    [LoggerMessage(2000, LogLevel.Information, "Beginning async initialization of {initialCount} initializer(s).")]
    public static partial void LogBeginningAsyncInitialization(this ILogger logger, int initialCount);

    [LoggerMessage(2001, LogLevel.Warning, "Application stopping requested before initialization completed. {remaining} initializer(s) did not complete.")]
    public static partial void LogApplicationStoppingBeforeInitialization(this ILogger logger, int remaining);

    [LoggerMessage(2002, LogLevel.Debug, "Starting initialization pass. Remaining: {remaining}")]
    public static partial void LogStartingInitializationPass(this ILogger logger, int remaining);

    [LoggerMessage(2003, LogLevel.Debug, "Attempting initializer {initializerType}")]
    public static partial void LogAttemptingInitializer(this ILogger logger, string initializerType);

    [LoggerMessage(2012, LogLevel.Debug, "Initializer {initializerType} started.")]
    public static partial void LogInitializerStarted(this ILogger logger, string initializerType);

    [LoggerMessage(2013, LogLevel.Information, "Initializer {initializerType} completed in {elapsedMs}ms.")]
    public static partial void LogInitializerCompleted(this ILogger logger, string initializerType, double elapsedMs);

    [LoggerMessage(2004, LogLevel.Information, "Initialized {initializerType}. Remaining: {remaining}/{initialCount}")]
    public static partial void LogInitialized(this ILogger logger, string initializerType, int remaining, int initialCount);

    [LoggerMessage(2005, LogLevel.Warning, "Initialization of {initializerType} delayed: {message}")]
    public static partial void LogInitializationDelayed(this ILogger logger, Exception ex, string initializerType, string message);

    [LoggerMessage(2014, LogLevel.Warning, "Initialization of {initializerType} delayed after {elapsedMs}ms: {message}")]
    public static partial void LogInitializationDelayedWithElapsed(this ILogger logger, Exception ex, string initializerType, double elapsedMs, string message);

    [LoggerMessage(2006, LogLevel.Information, "All {initialCount} initializer(s) completed successfully.")]
    public static partial void LogAllInitializersCompleted(this ILogger logger, int initialCount);

    [LoggerMessage(2007, LogLevel.Error, "Initialization made no progress this pass. {remaining} initializer(s) are stuck: {stuckList}")]
    public static partial void LogNoProgress(this ILogger logger, int remaining, string stuckList);

    [LoggerMessage(2008, LogLevel.Debug, "Pass completed. ProgressCount: {progressCount}. Remaining: {remaining}")]
    public static partial void LogPassCompleted(this ILogger logger, int progressCount, int remaining);

    [LoggerMessage(2009, LogLevel.Critical, "Failed to complete initialization. {remaining} of {initialCount} are stuck: {stuckList}")]
    public static partial void LogFailedToCompleteInitialization(this ILogger logger, int remaining, int initialCount, string stuckList);

    [LoggerMessage(2010, LogLevel.Information, "Service readiness set to true.")]
    public static partial void LogServiceReadinessSet(this ILogger logger);

    [LoggerMessage(2011, LogLevel.Critical, "Failed to initialize services: {message}")]
    public static partial void LogFailedToInitializeServices(this ILogger logger, Exception ex, string message);

    [LoggerMessage(2015, LogLevel.Debug, "Failed to instantiate type {type} for key '{key}'")]
    public static partial void LogFailedToInstantiateType(this ILogger logger, string type, string? key);

    [LoggerMessage(2016, LogLevel.Warning, "Failed to resolve keyed service of type {type} with key {key}. Will retry later. Message: {message}")]
    public static partial void LogFailedToResolveKeyedService(this ILogger logger, string type, string key, string message);

    [LoggerMessage(2017, LogLevel.Warning, "Failed to resolve service of type {type}. Will retry later. Message: {message}")]
    public static partial void LogFailedToResolveService(this ILogger logger, string type, string message);
}
