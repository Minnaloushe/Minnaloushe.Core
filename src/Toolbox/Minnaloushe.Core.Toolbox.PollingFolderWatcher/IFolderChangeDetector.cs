namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

/// <summary>
/// Detects changes in a monitored folder by comparing directory snapshots and filtering stable files.
/// </summary>
internal interface IFolderChangeDetector
{
    /// <summary>
    /// Performs a single poll cycle: detects changes, filters stable files, and processes events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PollAsync(CancellationToken cancellationToken);
}
