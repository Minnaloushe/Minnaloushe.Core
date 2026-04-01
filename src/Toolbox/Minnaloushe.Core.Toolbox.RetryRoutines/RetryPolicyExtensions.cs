using Minnaloushe.Core.Toolbox.RetryRoutines.Options;

namespace Minnaloushe.Core.Toolbox.RetryRoutines;

public static class RetryPolicyExtensions
{
    public static TimeSpan GetDelay(this RetryPolicyOptions options, int attempt)
    {
        return options.Type switch
        {
            RetryPolicyType.ExponentialBackoff => TimeSpan.FromMilliseconds(options.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)),
            RetryPolicyType.FixedDelay => options.InitialDelay,
            _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported retry policy type: {options.Type}")
        };
    }
}