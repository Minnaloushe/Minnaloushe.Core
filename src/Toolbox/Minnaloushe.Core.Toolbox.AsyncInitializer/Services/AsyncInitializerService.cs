using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ReadinessProbe.Abstractions;
using Minnaloushe.Core.Toolbox.AsyncInitializer.KeyedInitializer;
using Minnaloushe.Core.Toolbox.StringExtensions;
using System.Diagnostics;

namespace Minnaloushe.Core.Toolbox.AsyncInitializer.Services;

internal class AsyncInitializerService(
    IServiceProvider sp,
    IHostApplicationLifetime hostApplicationLifetime,
    IReadinessProbe<AsyncInitializerService> readinessProbe,
    IKeyedInitializerRegistry keyedInitializerRegistry,
    ILogger<AsyncInitializerService> logger
)
{
    public async Task InitializeAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var initializers = sp.GetServices<IAsyncInitializer>()
                .Select(i =>
                    new DeferredInitialiserInstance(null, i.GetType())
                    {
                        Instance = i
                    }
                );

            var initializersList = initializers.ToList();

            foreach (var (key, type) in keyedInitializerRegistry.Registry)
            {
                initializersList.Add(new DeferredInitialiserInstance
                (
                    key,
                    type
                ));
            }

            var initialCount = initializersList.Count;

            logger.LogBeginningAsyncInitialization(initialCount);

            var exceptions = new List<Exception>();
            // Loop while there are items and cancellation has not been requested.
            while (initializersList.Count > 0)
            {
                exceptions.Clear();

                cancellationToken.ThrowIfCancellationRequested();
                if (hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    logger.LogApplicationStoppingBeforeInitialization(initializersList.Count);
                    break;
                }

                var progressCount = 0;
                logger.LogStartingInitializationPass(initializersList.Count);

                for (var i = initializersList.Count - 1; i >= 0; i--)
                {
                    var asyncInitializer = initializersList[i];
                    var initializerType = asyncInitializer.Type.FullName ?? asyncInitializer.Type.Name;

                    logger.LogAttemptingInitializer(initializerType);

                    var swInit = Stopwatch.StartNew();
                    try
                    {
                        // Measure and log per-initializer duration to diagnose slow initializers.
                        logger.LogInitializerStarted(initializerType);

                        var instance = TrySafeResolve(asyncInitializer);

                        if (instance == null)
                        {
                            logger.LogFailedToInstantiateType(asyncInitializer.Type.GetFriendlyName(),
                                asyncInitializer.Key?.ToString());
                            continue;
                        }

                        // Keep this call consistent with your IAsyncInitializer signature.
                        var result = await instance.InitializeAsync(cancellationToken).ConfigureAwait(false);

                        if (!result)
                        {
                            continue;
                        }

                        swInit.Stop();
                        initializersList.RemoveAt(i);
                        progressCount++;

                        logger.LogInitialized(initializerType, initializersList.Count, initialCount);
                        logger.LogInitializerCompleted(initializerType, swInit.Elapsed.TotalMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        swInit.Stop();
                        exceptions.Add(ex);
                        logger.LogInitializationDelayedWithElapsed(ex, initializerType, 0d, ex.Message);
                    }
                }

                if (initializersList.Count == 0)
                {
                    logger.LogAllInitializersCompleted(initialCount);
                    break;
                }

                if (progressCount == 0)
                {
                    var stuckList = string.Join(", ", initializersList.Select(i => i.GetType().GetFriendlyName()));
                    logger.LogNoProgress(initializersList.Count, stuckList);
                    throw new InvalidOperationException($"Failed to complete initialization. {initializersList.Count} of {initialCount} are stuck: {stuckList}", new AggregateException(exceptions));
                }

                logger.LogPassCompleted(progressCount, initializersList.Count);
            }

            if (initializersList.Count > 0)
            {
                var stuckList = string.Join(", ", initializersList.Select(i => i.GetType().GetFriendlyName()));
                logger.LogFailedToCompleteInitialization(initializersList.Count, initialCount, stuckList);
                throw new InvalidOperationException($"Failed to complete initialization. {initializersList.Count} of {initialCount} are stuck: {stuckList}", new AggregateException(exceptions));
            }

            logger.LogServiceReadinessSet();
        }
        catch (Exception ex)
        {
            logger.LogFailedToInitializeServices(ex, ex.Message);
            hostApplicationLifetime.StopApplication();
            throw;
        }

        readinessProbe.SetState(HealthStatus.Healthy);
    }

    private record DeferredInitialiserInstance(object? Key, Type Type)
    {
        public IAsyncInitializer? Instance { get; set; }
    };

    private IAsyncInitializer? TrySafeResolve(DeferredInitialiserInstance instance)
    {
        if (instance.Instance is not null)
        {
            return instance.Instance;
        }

        if (instance.Key is not null)
        {
            try
            {
                instance.Instance = sp.GetRequiredKeyedService(instance.Type, instance.Key) as IAsyncInitializer;
                return instance.Instance;
            }
            catch (Exception ex)
            {
                logger.LogFailedToResolveKeyedService(instance.Type.FullName ?? instance.Type.Name,
                    instance.Key.ToString() ?? string.Empty, ex.Message);
                return null;
            }
        }

        try
        {
            instance.Instance = sp.GetRequiredService(instance.Type) as IAsyncInitializer;
            return instance.Instance;
        }
        catch (Exception ex)
        {
            logger.LogFailedToResolveService(instance.Type.FullName ?? instance.Type.Name, ex.Message);
            return null;
        }
    }
}