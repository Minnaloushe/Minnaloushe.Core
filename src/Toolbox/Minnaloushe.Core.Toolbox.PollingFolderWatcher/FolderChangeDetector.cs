using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

/// <summary>
/// Detects changes in a monitored folder by comparing directory snapshots and filtering stable files.
/// Maintains state between polls to track new, modified, and deleted files.
/// </summary>
internal sealed class FolderChangeDetector : IFolderChangeDetector
{

    private readonly FolderWatcherOptions _options;
    private readonly IFileSystemAccessor _fileSystemAccessor;
    private readonly ILogger<FolderChangeDetector> _logger;
    private readonly Regex _maskRegex;

    private Dictionary<string, FileInfoSnapshot> _storedDirectorySnapshot = [];
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public FolderChangeDetector(
        IOptions<FolderWatcherOptions> options,
        IFileSystemAccessor fileSystemAccessor,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<FolderChangeDetector> logger)
    {
        _options = options.Value;
        _fileSystemAccessor = fileSystemAccessor;
        _logger = logger;
        _maskRegex = new Regex(_options.MaskRegex, RegexOptions.Compiled);
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task PollAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<IFolderWatcherHandler>();

        // 1. Gather current directory snapshot
        var currentDirectorySnapshot = (await _fileSystemAccessor.GetFiles(_options.Path, _options.EnumerationOptions))
            .Where(f => _maskRegex.IsMatch(f.Name))
            .ToDictionary(s => s.FullName, s => s);

        // 2. Detect all changes (new, modified, deleted)
        var allDetectedEvents = DetectChanges(currentDirectorySnapshot);

        // 3. Filter out blocked files (check stability)
        var stableEvents = await FilterStableFilesAsync(allDetectedEvents, cancellationToken);

        // 4. Call handler for each stable event
        await ProcessEventsAsync(handler, stableEvents, cancellationToken);

        // 5. Store current snapshot for next loop (only stable files)
        UpdateStoredSnapshot(currentDirectorySnapshot, allDetectedEvents, stableEvents);
    }

    private List<FileChangedEventArgs> DetectChanges(Dictionary<string, FileInfoSnapshot> currentSnapshot)
    {
        var events = new List<FileChangedEventArgs>();

        foreach (var (filePath, snapshot) in currentSnapshot)
        {
            if (!_storedDirectorySnapshot.TryGetValue(filePath, out var known))
            {
                events.Add(new FileChangedEventArgs(snapshot, ChangeEventType.New));
            }
            else if (snapshot.ModifiedAt != known.ModifiedAt || snapshot.Length != known.Length)
            {
                events.Add(new FileChangedEventArgs(snapshot, ChangeEventType.Modified));
            }
        }

        var deletedKeys = _storedDirectorySnapshot.Keys.Except(currentSnapshot.Keys);

        events.AddRange(deletedKeys.Select(deletedKey =>
            new FileChangedEventArgs(_storedDirectorySnapshot[deletedKey], ChangeEventType.Deleted)));

        return events;
    }

    private async Task<List<FileChangedEventArgs>> FilterStableFilesAsync(List<FileChangedEventArgs> events, CancellationToken token)
    {
        var stableEvents = new List<FileChangedEventArgs>();

        foreach (var evt in events)
        {
            if (evt.EventType == ChangeEventType.Deleted)
            {
                stableEvents.Add(evt);
                continue;
            }

            if (await _fileSystemAccessor.CheckForFileWriteCompletionAsync(evt.FileInfo, _options.WriteCompletionCheckAttempts, _options.WriteCompletionCheckWaitDelay, token))
            {
                stableEvents.Add(evt);
            }
            else
            {
                _logger.FileNotStable(evt.FileInfo.FullName);
            }
        }

        return stableEvents;
    }

    private async Task ProcessEventsAsync(IFolderWatcherHandler handler, List<FileChangedEventArgs> events,
        CancellationToken token)
    {
        foreach (var evt in events)
        {
            try
            {
                using var loggerScope = _logger.BeginScope("Processing file change: {FilePath} ({EventType})", evt.FileInfo.FullName, evt.EventType);

                await handler.HandleFileChange(
                    new FileChangedEventArgs(evt.FileInfo, evt.EventType),
                    token);
            }
            catch (Exception ex)
            {
                LogHandlerError(ex, evt.FileInfo.FullName, evt.EventType);
            }
        }
    }

    private void LogHandlerError(Exception ex, string filePath, ChangeEventType eventType)
    {
        switch (eventType)
        {
            case ChangeEventType.New:
                _logger.ErrorHandlingNewFile(ex, filePath);
                break;
            case ChangeEventType.Modified:
                _logger.ErrorHandlingModifiedFile(ex, filePath);
                break;
            case ChangeEventType.Deleted:
                _logger.ErrorHandlingDeletedFile(ex, filePath);
                break;
        }
    }

    private void UpdateStoredSnapshot(
        Dictionary<string, FileInfoSnapshot> currentSnapshot,
        List<FileChangedEventArgs> allDetectedEvents,
        List<FileChangedEventArgs> stableEvents)
    {
        var unstableFilePaths = allDetectedEvents
            .Where(e => e.EventType != ChangeEventType.Deleted)
            .Select(e => e.FileInfo.FullName)
            .Except(stableEvents.Where(e => e.EventType != ChangeEventType.Deleted).Select(e => e.FileInfo.FullName))
            .ToHashSet();

        var newSnapshot = new Dictionary<string, FileInfoSnapshot>();

        foreach (var (filePath, snapshot) in currentSnapshot)
        {
            if (!unstableFilePaths.Contains(filePath))
            {
                newSnapshot[filePath] = snapshot;
            }
        }

        _storedDirectorySnapshot = newSnapshot;
    }
}
