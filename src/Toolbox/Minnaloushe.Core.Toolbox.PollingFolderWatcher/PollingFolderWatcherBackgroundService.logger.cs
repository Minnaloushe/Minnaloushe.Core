using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

internal static partial class PollingFolderWatcherBackgroundServiceLogger
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Started monitoring path {Path}")]
    internal static partial void StartedMonitoring(this ILogger<PollingFolderWatcherBackgroundService> logger, string path);


    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Finished polling directory changes. Next run at {NextRun}")]
    internal static partial void FinishedPolling(this ILogger<PollingFolderWatcherBackgroundService> logger, DateTimeOffset nextRun);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Failed to pull files from directory {Path}")]
    internal static partial void FailDuringMainLoopExecution(this ILogger<PollingFolderWatcherBackgroundService> logger, Exception ex, string path);
}