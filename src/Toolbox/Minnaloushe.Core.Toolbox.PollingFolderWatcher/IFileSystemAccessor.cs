namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

public interface IFileSystemAccessor
{
    Task<bool> DirectoryExists(string path);
    Task<IReadOnlyCollection<FileInfoSnapshot>> GetFiles(string path, EnumerationOptions options);

    Task<bool> CheckForFileWriteCompletionAsync(FileInfoSnapshot fileInfo, int attempts, TimeSpan waitDelay,
        CancellationToken token);

    void CreateDirectory(string valuePath);
}