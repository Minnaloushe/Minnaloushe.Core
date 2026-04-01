namespace Minnaloushe.Core.ServiceDiscovery.Routines;

public interface IInfrastructureConventionProvider
{
    Task<string> GetApplicationName();
    Task<string> GetApplicationNamespace();
    Task<string> GetDatabaseRole(string databaseServiceName, string roleName);
    Task<string> GetConsulServiceName(string databaseServiceName);
    Task<string> GetStaticSecretPath(string serviceName);
    Task<string> GetKvSecretPath(string path);
}