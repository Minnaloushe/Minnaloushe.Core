namespace Minnaloushe.Core.Toolbox.RetryRoutines.Options;

public record RetryPolicyOptions
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.MaxValue;
    public RetryPolicyType Type { get; init; } = RetryPolicyType.ExponentialBackoff;
}