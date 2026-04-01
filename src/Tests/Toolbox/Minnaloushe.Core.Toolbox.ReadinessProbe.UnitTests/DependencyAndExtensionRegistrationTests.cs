using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Minnaloushe.Core.ReadinessProbe;
using Minnaloushe.Core.ReadinessProbe.Abstractions;
using Shouldly;

namespace Minnaloushe.Core.Toolbox.ReadinessProbe.UnitTests;

[TestFixture]
public class DependencyAndExtensionRegistrationTests
{
    [Test]
    public void AddReadinessProbe_RegistersReadinessHealthCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddReadinessProbes();

        // Assert - verify that a registration for IHealthCheck that references ReadinessProbeHealthCheck exists

        services.Any(sd => sd.ServiceType == typeof(HealthCheckService) && sd?.ImplementationType?.Name == "DefaultHealthCheckService")
            .ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(IHostedService) && sd?.ImplementationType?.Name == "HealthCheckPublisherHostedService")
            .ShouldBeTrue();
    }

    [Test]
    public void AddSingletonAsReadinessProbe_RegistersServicesAndResolvesExpectedInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSingletonAsReadinessProbe<DummyService>();
        var provider = services.BuildServiceProvider();

        // Resolve the concrete service TImplementation
        var impl = provider.GetService<DummyService>();
        impl.ShouldNotBeNull();

        // Resolve the generic IReadinessProbe<DummyService>
        var genericProbe = provider.GetService<IReadinessProbe<DummyService>>();
        genericProbe.ShouldNotBeNull();

        // Resolve the non-generic IReadinessProbe
        var nonGenericProbe = provider.GetService<IReadinessProbe>();
        nonGenericProbe.ShouldNotBeNull();

        // They should all refer to the same readiness probe instance when cast appropriately
        // (non-generic should be the same instance as generic)
        nonGenericProbe.ShouldBe(genericProbe);
    }
}