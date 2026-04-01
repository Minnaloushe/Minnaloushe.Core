using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Options;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Services;
using Minnaloushe.Core.Toolbox.Cancellation;
using System.Diagnostics;

namespace Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;

public static class InitializationExtensions
{
    /// <summary>
    /// Invokes all registered <c>IAsyncInitializer</c> implementations using a scoped
    /// service provider. The method enforces a startup timeout, respects the
    /// application's cancellation context, and logs lifecycle events.
    /// Must be called before app.RunAsync().
    /// </summary>
    /// <param name="app">The host whose services will be used to resolve initializers.</param>
    /// <returns>A <see cref="Task"/> that completes when initialization has finished or failed.</returns>
    public static async Task InvokeAsyncInitializers(this IHost app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AsyncInitializer");
        try
        {
            using var initScope = app.Services.CreateScope();
            // Use GetService so we get null if not registered (GetRequiredService would throw)
            var invoker = initScope.ServiceProvider.GetService<AsyncInitializerService>();
            if (invoker is null)
            {
                logger.LogNoInitializers();
                return;
            }

            var options = initScope.ServiceProvider.GetRequiredService<IOptions<AsyncInitializerOptions>>();

            var sw = Stopwatch.StartNew();
            // Example: enforce a startup timeout (adjust as appropriate)
            var timeout = options.Value.Timeout;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationContext.Current);
            cts.CancelAfter(timeout);

            try
            {
                // Ensure the initializer accepts a cancellation token (modify InitializeAllAsync if needed)
                await invoker.InitializeAllAsync(cts.Token).ConfigureAwait(false);
                sw.Stop();
                logger.LogAsyncInitializationCompleted(sw.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException) when (CancellationContext.Current.IsCancellationRequested)
            {
                logger.LogAsyncInitializationCancelled();
                throw;
            }
            catch (OperationCanceledException)
            {
                logger.LogAsyncInitializationTimedOut(timeout.TotalSeconds);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogAsyncInitializationFailed(ex);
            throw;
        }
    }
}