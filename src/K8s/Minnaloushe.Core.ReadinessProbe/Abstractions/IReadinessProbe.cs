using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Minnaloushe.Core.ReadinessProbe.Abstractions;

public interface IReadinessProbe<T> : IReadinessProbe
{
    void SetState(HealthStatus status);
}

public interface IReadinessProbe
{
    HealthStatus CurrentStatus { get; }
}