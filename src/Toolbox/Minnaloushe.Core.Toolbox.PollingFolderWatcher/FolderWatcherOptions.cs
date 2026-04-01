namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

public record FolderWatcherOptions
{
    internal static readonly string SectionName = "FolderWatcher";
    public string Path { get; init; } = ".";
    public TimeSpan Interval { get; init; }
    public string MaskRegex { get; init; } = ".*";
    public EnumerationOptions EnumerationOptions { get; set; } = new();
    public TimeSpan WriteCompletionCheckWaitDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public int WriteCompletionCheckAttempts { get; set; } = 3;
    public bool ForceCreateDirectory { get; init; } = true;
}