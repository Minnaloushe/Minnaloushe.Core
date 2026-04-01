using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ReadinessProbe.Implementations;

namespace Minnaloushe.Core.ReadinessProbe;

public static class DependencyRegistration
{
    public static IServiceCollection AddReadinessProbes(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<ReadinessProbeHealthCheck>("readiness", tags: ["ready"]);

        return services;
    }

    public static void MapReadinessProbes(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => !check.Tags.Contains("ready")
        }).ExcludeFromDescription();

        endpoints.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).ExcludeFromDescription();
    }
}