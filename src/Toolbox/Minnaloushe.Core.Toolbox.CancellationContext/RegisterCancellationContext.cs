using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Minnaloushe.Core.Toolbox.Cancellation;

public static class RegisterCancellationTokenContext
{
    public static T UseCancellationContext<T>(this T app, Action<CancellationContextOptions>? options = null)
        where T : IHost, IApplicationBuilder
    {
        //TODO Amend, make more flexible
        var opts = new CancellationContextOptions();

        options?.Invoke(opts);
        //if (opts is { RequestTimeout: { Ticks: > 0 }, UseMiddleware: false })
        //{
        //    throw new ArgumentException("RequestTimeout has no use without UseMiddleware", nameof(options));
        //}

        if (opts.UseMiddleware)
        {
            app.UseMiddleware<CancellationMiddleware>(opts.RequestTimeout);
        }

        CancellationContext.Initialize(app.Services.GetRequiredService<IHostApplicationLifetime>());
        return app;
    }
}