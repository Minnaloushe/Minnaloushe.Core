namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

internal class FileSystemAccessor : IFileSystemAccessor
{
    public Task<bool> DirectoryExists(string path) => Task.FromResult(Directory.Exists(path));

    public Task<IReadOnlyCollection<FileInfoSnapshot>> GetFiles(string path, EnumerationOptions options)
    {
        return Task.FromResult<IReadOnlyCollection<FileInfoSnapshot>>(
        [
            ..new DirectoryInfo(path)
                .GetFiles("*.*", options)
                .Select(i => new FileInfoSnapshot
                {
                    Name = i.Name,
                    FullName = i.FullName,
                    Length = i.Length,
                    CreatedAt = i.CreationTimeUtc,
                    ModifiedAt = i.LastWriteTimeUtc
                })
        ]);
    }

    public async Task<bool> CheckForFileWriteCompletionAsync(FileInfoSnapshot fileInfo, int attempts, TimeSpan waitDelay, CancellationToken token)
    {
        try
        {
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                try
                {
                    await using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                await Task.Delay(attempts, token).ConfigureAwait(false);
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void CreateDirectory(string valuePath)
    {
        Directory.CreateDirectory(valuePath);
    }
}