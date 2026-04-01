namespace Minnaloushe.Core.Toolbox.AsyncInitializer.Options;

public record AsyncInitializerOptions
{
    public static readonly string SectionName = "AsyncInitializer";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(1);
}
