using DotNet.Testcontainers.Builders;
using System.Net;

namespace Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

public class ConsulContainerWrapper : ContainerWrapperBase
{
    protected override ContainerBuilder InitContainer(ContainerBuilder builder)
    {
        return builder
            .WithPortBinding(ContainerPort, true)
            .WithCommand("agent", "-dev", "-client=0.0.0.0")
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request =>
                        request.ForPort(ContainerPort)
                            .ForPath("/v1/status/leader")
                            .ForStatusCode(HttpStatusCode.OK)));
    }

    protected override string ImageName => "hashicorp/consul:1.15";
    protected override ushort ContainerPort => 8500;

    public async Task ConfigureRegistration(IContainerWrapper container, string[]? tags = null)
    {
        using var httpClient = new HttpClient();
        var consulAddress = new UriBuilder(
            Uri.UriSchemeHttp,
            Instance.Hostname,
            Instance.GetMappedPublicPort(ContainerPort)
        ).Uri.ToString().TrimEnd('/');

        var host = container.Host;
        var port = container.Port;

        var serviceRegistration = new
        {
            ID = container.Name,
            Name = container.Name,
            Address = host,
            Port = port,
            Tags = tags
        };

        var response = await httpClient.PutAsync(
            $"{consulAddress}/v1/agent/service/register",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(serviceRegistration), System.Text.Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to register service '{container.Name}' in Consul: {errorContent}");
        }
    }

}