namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

public interface IFolderWatcherHandler
{
    Task HandleFileChange(FileChangedEventArgs args, CancellationToken cancellationToken);
}