using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.MongoDb.Factories;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Extensions;

public static class MongoClientExtensions
{
    public static IServiceCollection AddMongoRegistration(this IServiceCollection services)
    {
        services.AddSingleton<IMongoClientFactory, MongoClientFactory>();
        services.AddSingleton<IRenewableClientHolder<IMongoClient>, RenewableClientHolder<IMongoClient>>();

        return services;
    }
}