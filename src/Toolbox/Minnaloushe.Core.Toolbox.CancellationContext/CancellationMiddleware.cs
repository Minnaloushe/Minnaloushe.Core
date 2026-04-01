using Microsoft.AspNetCore.Http;

namespace Minnaloushe.Core.Toolbox.Cancellation;

/// <summary>
///     Middleware to capture RequestAborted cancellation token
/// </summary>
/// <param name="next"></param>
internal class CancellationMiddleware(RequestDelegate next, TimeSpan? timeout = null)
{
    public async Task InvokeAsync(HttpContext context)
    {
        CancellationContext.SetToken(timeout, context.RequestAborted);
        try
        {
            await next(context);
        }
        finally
        {
            CancellationContext.Clear(); // Ensure disposal after request
        }
    }
}