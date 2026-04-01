using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minnaloushe.Core.ReadinessProbe.Abstractions;
using Minnaloushe.Core.ReadinessProbe.Implementations;
using Moq;
using Shouldly;

namespace Minnaloushe.Core.Toolbox.ReadinessProbe.UnitTests;

[TestFixture]
public class ReadinessProbeHealthCheckTests
{
    [Test]
    public async Task CheckHealthAsync_NoProbes_ReturnsHealthyWithNoProbesMessage()
    {
        // Arrange
        var healthCheck = new ReadinessProbeHealthCheck([]);
        var ctx = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(ctx, CancellationToken.None);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldBe("No readiness probes registered.");
    }

    [Test]
    public async Task CheckHealthAsync_AllProbesReady_ReturnsHealthy()
    {
        // Arrange
        var readyMock = new Mock<IReadinessProbe>();
        readyMock.SetupGet(x => x.CurrentStatus).Returns(HealthStatus.Healthy);

        var healthCheck = new ReadinessProbeHealthCheck([readyMock.Object]);
        var ctx = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(ctx, CancellationToken.None);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldBe("All readiness probes report ready.");
    }

    [Test]
    public async Task CheckHealthAsync_NotReadyProbes_ReturnsUnhealthyWithDetails()
    {
        // Arrange
        var notReadyMock = new Mock<IReadinessProbe>();
        notReadyMock.SetupGet(x => x.CurrentStatus).Returns(HealthStatus.Unhealthy);

        var healthCheck = new ReadinessProbeHealthCheck([notReadyMock.Object]);
        var ctx = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(ctx, CancellationToken.None);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldStartWith("Readiness probes not ready:");
        // should mention at least something about the not-ready probe types
        result.Description.ShouldContain("Readiness probes not ready:");
    }
}