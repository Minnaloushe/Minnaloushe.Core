using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

/// <summary>
/// Provides a background service that monitors a specified folder for file changes by periodically polling the
/// directory and notifies a handler of new, modified, or deleted files.
/// Not optimal, might produce overhead on large directories, but works in environments where event-based monitoring is not available or reliable.
/// </summary>
/// <remarks>This service uses a polling approach to detect file changes, which may result in a delay between when
/// a change occurs and when it is detected, depending on the configured polling interval. It is suitable for scenarios
/// where event-based file system monitoring is not available or not reliable. The service filters files using the
/// provided regular expression mask and only notifies the handler of changes that match the mask.
/// Includes write-completion detection to avoid processing files that are still being written (locked by another process).
/// Files that fail the stability check are skipped and will be re-evaluated in the next poll.
/// </remarks>
/// <param name="detector">The folder change detector that performs the actual change detection logic. Cannot be null.</param>
/// <param name="options">The configuration options that specify the folder path, file mask, polling interval, and enumeration behavior.
/// Cannot be null.</param>
/// <param name="fileSystemAccessor">The file system accessor for checking directory existence. Cannot be null.</param>
/// <param name="logger">The logger used to record monitoring activity and errors. Cannot be null.</param>
internal sealed class PollingFolderWatcherBackgroundService(
    IFolderChangeDetector detector,
    IOptions<FolderWatcherOptions> options,
    IFileSystemAccessor fileSystemAccessor,
    ILogger<PollingFolderWatcherBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.ForceCreateDirectory)
        {
            fileSystemAccessor.CreateDirectory(options.Value.Path);
        }
        else if (!await fileSystemAccessor.DirectoryExists(options.Value.Path))
        {
            throw new ArgumentException($"Directory '{options.Value.Path}' does not exist", nameof(options));
        }

        logger.StartedMonitoring(options.Value.Path);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await detector.PollAsync(stoppingToken);
                logger.FinishedPolling(DateTimeOffset.UtcNow.Add(options.Value.Interval));
            }
            catch (Exception ex)
            {
                logger.FailDuringMainLoopExecution(ex, options.Value.Path);
            }

            await Task.Delay(options.Value.Interval, stoppingToken);
        }
    }
}
