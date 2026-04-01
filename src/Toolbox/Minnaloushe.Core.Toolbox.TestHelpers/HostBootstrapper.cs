using Microsoft.Extensions.Hosting;

namespace Minnaloushe.Core.Toolbox.TestHelpers;

public static class HostBootstrapper
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host
            .CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                // Production configuration
                // cfg.AddJsonFile(...)
            })
            .ConfigureServices((ctx, services) =>
            {
            });
    }
}