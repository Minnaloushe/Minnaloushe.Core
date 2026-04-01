namespace Minnaloushe.Core.Toolbox.TestHelpers;

public static class TaskCompletionSourceExtensions
{
    public static async Task<T?> WaitAsync<T>(this TaskCompletionSource<T>? tcs, int delayInSeconds, CancellationToken ct = default)
    {
        return tcs == null
            ? default
            : System.Diagnostics.Debugger.IsAttached
            ? await tcs.Task.WaitAsync(Timeout.InfiniteTimeSpan, ct)
            : await tcs.Task.WaitAsync(TimeSpan.FromSeconds(delayInSeconds), ct);
    }
}
