namespace Minnaloushe.Core.Toolbox.Cancellation;

public record CancellationContextOptions
{
    public bool UseMiddleware { get; set; } = false;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(1);
}