using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Minnaloushe.Core.Toolbox.TestHelpers;

public sealed class TestHost : IAsyncDisposable
{
    private readonly IHost _host;

    public IServiceProvider Services => _host.Services;
    public IConfiguration Configuration { get; }

    private TestHost(IHost host, IConfiguration configuration)
    {
        _host = host;
        Configuration = configuration;
    }

    public static async Task<TestHost> Build(
        Action<IServiceCollection, IConfiguration>? configureServices = null,
        Action<IConfigurationBuilder>? configureConfiguration = null,
        Func<IHost, Task>? beforeStart = null,
        bool startHost = true)
    {
        var configurationBuilder = new ConfigurationBuilder();
        configureConfiguration?.Invoke(configurationBuilder);

        var configuration = configurationBuilder.Build();

        var hostBuilder = HostBootstrapper
            .CreateHostBuilder([])
            .ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddConfiguration(configuration);
            })
            .ConfigureServices((services) =>
            {
                configureServices?.Invoke(services, configuration);
            });

        var host = hostBuilder.Build();

        if (beforeStart != null)
        {
            await beforeStart.Invoke(host);
        }

        if (startHost)
        {
            host.Start();
        }

        return new TestHost(host, host.Services.GetRequiredService<IConfiguration>());
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        //_host.Dispose();
    }
}