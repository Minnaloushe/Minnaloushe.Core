using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minnaloushe.Core.ReadinessProbe.Abstractions;

namespace Minnaloushe.Core.ReadinessProbe.Implementations;

internal sealed class ReadinessProbeHealthCheck(IEnumerable<IReadinessProbe> probes) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var probesList = probes.ToList();

        if (probesList.Count == 0)
        {
            // No readiness probes registered — consider healthy (or change to Unhealthy if you prefer)
            return Task.FromResult(HealthCheckResult.Healthy("No readiness probes registered."));
        }

        var notReady = probesList.Where(p => p.CurrentStatus == HealthStatus.Unhealthy).ToList();

        if (notReady.Count > 0)
        {
            var details = string.Join(", ", notReady.Select(p => p.GetType().FullName ?? p.GetType().Name));
            return Task.FromResult(HealthCheckResult.Unhealthy($"Readiness probes not ready: {details}"));
        }

        var degraded = probesList.Where(p => p.CurrentStatus == HealthStatus.Degraded).ToList();

        if (degraded.Count > 0)
        {
            var details = string.Join(", ", degraded.Select(p => p.GetType().FullName ?? p.GetType().Name));
            return Task.FromResult(HealthCheckResult.Degraded($"Readiness probes degraded: {details}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All readiness probes report ready."));
    }
}