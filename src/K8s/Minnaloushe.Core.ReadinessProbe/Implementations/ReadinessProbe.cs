using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ReadinessProbe.Abstractions;

namespace Minnaloushe.Core.ReadinessProbe.Implementations;

internal class ReadinessProbe<T>(ILogger<T> logger) : IReadinessProbe<T>
{
    public HealthStatus CurrentStatus { get; private set; }

    public void SetState(HealthStatus status)
    {
        logger.LogClassReportedStateState(typeof(T).Name, status);
        CurrentStatus = status;
    }
}