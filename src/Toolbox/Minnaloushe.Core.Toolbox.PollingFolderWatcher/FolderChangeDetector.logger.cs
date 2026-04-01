using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

internal static partial class FolderChangeDetectorLogger
{
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error handling new file {FileName}")]
    internal static partial void ErrorHandlingNewFile(this ILogger<FolderChangeDetector> logger, Exception ex, string fileName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error handling modified file {FileName}")]
    internal static partial void ErrorHandlingModifiedFile(this ILogger<FolderChangeDetector> logger, Exception ex, string fileName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error handling deleted file {FileName}")]
    internal static partial void ErrorHandlingDeletedFile(this ILogger<FolderChangeDetector> logger, Exception ex, string fileName);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "File {FileName} is not stable yet; will check again in the next poll.")]
    internal static partial void FileNotStable(this ILogger<FolderChangeDetector> logger, string fileName);
}