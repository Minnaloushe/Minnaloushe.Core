namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

public record FileInfoSnapshot
{
    public string Name { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public long Length { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
}