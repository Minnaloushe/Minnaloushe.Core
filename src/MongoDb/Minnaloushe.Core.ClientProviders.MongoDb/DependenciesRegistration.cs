using Microsoft.Extensions.DependencyInjection;

namespace Minnaloushe.Core.ClientProviders.MongoDb;

public static class DependenciesRegistration
{
    public static IServiceCollection AddMongoDbClientProviders(this IServiceCollection services)
    {
        services.AddMongoRegistration();
        return services;
    }
}