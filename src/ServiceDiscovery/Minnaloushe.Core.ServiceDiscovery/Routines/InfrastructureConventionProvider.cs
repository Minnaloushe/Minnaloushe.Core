using Minnaloushe.Core.Toolbox.StringExtensions;
using System.Reflection;

namespace Minnaloushe.Core.ServiceDiscovery.Routines;

//TODO: Consider moving this into a separate assembly and introducing explicit registration extension.
// This is not only about service discovery anymore, but also about general infrastructure conventions.
internal class InfrastructureConventionProvider : IInfrastructureConventionProvider
{
    public Task<string> GetApplicationName()
    {
        var appName = Environment.GetEnvironmentVariable("APP_NAME");
        if (appName.IsNotNullOrWhiteSpace())
        {
            return Task.FromResult(appName!);
        }

        var entry = Assembly.GetEntryAssembly()
            ?? Assembly.GetExecutingAssembly();

        var location = entry.Location;

        if (location.IsNotNullOrWhiteSpace())
        {
            return Task.FromResult(Path.GetFileNameWithoutExtension(location));
        }

        // Fallback to assembly simple name if Location is not available
        var name = entry.GetName().Name ?? string.Empty;

        return Task.FromResult(name);
    }
    public Task<string> GetApplicationNamespace()
    {
        var appNamespace = Environment.GetEnvironmentVariable("APP_NAMESPACE")
                           ?? string.Empty;

        return Task.FromResult(appNamespace);
    }
    public async Task<string> GetDatabaseRole(string databaseServiceName, string roleName)
    {
        var appNamespace = await GetApplicationNamespace();
        var appName = await GetApplicationName();
        return $"{appNamespace}-{databaseServiceName}-{roleName}";
    }

    public async Task<string> GetConsulServiceName(string databaseServiceName)
    {
        return $"{await GetApplicationNamespace()}-{databaseServiceName}";
    }

    public async Task<string> GetStaticSecretPath(string serviceName)
    {
        return $"{await GetApplicationNamespace()}/{serviceName}";
    }

    public async Task<string> GetKvSecretPath(string path)
    {
        return path.StartsWith('/')
            ? path
            : $"/{await GetApplicationNamespace()}/{path}";
    }
}