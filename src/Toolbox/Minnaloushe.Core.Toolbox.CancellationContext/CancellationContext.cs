using Microsoft.Extensions.Hosting;

namespace Minnaloushe.Core.Toolbox.Cancellation;

/// <summary>
///     Helper class to get HttpContext.RequestAborted and ApplicationStopping cancellation tokens when needed
/// </summary>
public static class CancellationContext
{
    private static readonly AsyncLocal<(CancellationToken Token, CancellationTokenSource Source)> Context = new();
    private static CancellationToken _shutdownToken;

    public static CancellationToken Current
    {
        get
        {
            var (token, _) = Context.Value;
            return token.CanBeCanceled ? token : _shutdownToken;
        }
    }

    internal static void Initialize(IHostApplicationLifetime lifetime)
    {
        _shutdownToken = lifetime.ApplicationStopping;
    }

    internal static void SetToken(TimeSpan? timeout, CancellationToken requestToken)
    {
        // Dispose the previous source if it exists
        Context.Value.Source?.Dispose();

        var timeoutCts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null;
        var timeoutToken = timeoutCts?.Token ?? CancellationToken.None;

        // Collect all cancellable tokens into an array
        var tokens = new[] { requestToken, _shutdownToken, timeoutToken }
            .Where(t => t.CanBeCanceled)
            .ToArray();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(tokens);
        Context.Value = (linkedCts.Token, linkedCts);
    }

    // Optional: Cleanup at end of scope
    internal static void Clear()
    {
        Context.Value.Source?.Dispose();
        Context.Value = default;
    }
}