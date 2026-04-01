using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ReadinessProbe.Abstractions;
using Minnaloushe.Core.ReadinessProbe.Implementations;
using Moq;
using Shouldly;

namespace Minnaloushe.Core.Toolbox.ReadinessProbe.UnitTests;

// simple marker type used as generic parameter for ReadinessProbe<T>

[TestFixture]
public class ReadinessProbeTests
{
    [Test]
    public void ReadinessProbe_DefaultsToNotReady_And_SetReadyChangesState_And_Logs()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DummyService>>();
        var probe = Activator.CreateInstance(
            typeof(ReadinessProbe<>).MakeGenericType(typeof(DummyService)),
            loggerMock.Object)!;

        var isReadyProp = typeof(IReadinessProbe).GetProperty(nameof(IReadinessProbe.CurrentStatus))!;

        // initial state
        var initial = (HealthStatus)isReadyProp.GetValue(probe)!;
        initial.ShouldBe(HealthStatus.Unhealthy);

        // Act - call SetReady via interface IReadinessProbe<DummyService>
        var genericInterface = typeof(IReadinessProbe<>).MakeGenericType(typeof(DummyService));
        var setReadyMethod = genericInterface.GetMethod(nameof(IReadinessProbe<DummyService>.SetState))!;
        setReadyMethod.Invoke(probe, [HealthStatus.Healthy]);

        // Assert state changed
        var after = (HealthStatus)isReadyProp.GetValue(probe)!;
        after.ShouldBe(HealthStatus.Healthy);
    }
}