namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

public record FileChangedEventArgs(FileInfoSnapshot FileInfo, ChangeEventType EventType);