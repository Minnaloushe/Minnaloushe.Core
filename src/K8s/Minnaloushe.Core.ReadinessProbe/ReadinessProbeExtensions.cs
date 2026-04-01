using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ReadinessProbe.Abstractions;
using Minnaloushe.Core.ReadinessProbe.Implementations;

namespace Minnaloushe.Core.ReadinessProbe;

public static class ReadinessProbeExtensions
{
    public static void AddSingletonAsReadinessProbe<TImplementation>(this IServiceCollection services)
        where TImplementation : class
    {
        services.AddSingleton<TImplementation>();
        services.AddSingleton<ReadinessProbe<TImplementation>>();
        services.AddSingleton<IReadinessProbe<TImplementation>>(sp =>
            sp.GetRequiredService<ReadinessProbe<TImplementation>>());
        services.AddSingleton<IReadinessProbe>(sp => sp.GetRequiredService<ReadinessProbe<TImplementation>>());
    }
}