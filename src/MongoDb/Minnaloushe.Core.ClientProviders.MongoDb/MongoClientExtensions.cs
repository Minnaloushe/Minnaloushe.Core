using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.MongoDb.Abstractions;
using Minnaloushe.Core.ClientProviders.MongoDb.Implementations;
using Minnaloushe.Core.ClientProviders.MongoDb.Models;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb;

public static class MongoClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMongoRegistration()
        {
            services.AddSingleton<IMongoClientFactory, MongoClientFactory>();
            services.AddSingleton<IRenewableClientHolder<IMongoClient>, RenewableClientHolder<IMongoClient>>();
        
            return services;
        }

        public IServiceCollection AddMongoConnection(string connectionName)
        {
            services.AddTransient<ICredentialsWatcher<MongoDbCredentials>, LeasedCredentialWatcher<MongoDbCredentials>>();
            services.AddKeyedSingleton<MongoClientProvider>(connectionName, (sp, key) => ActivatorUtilities.CreateInstance<MongoClientProvider>(sp, key!));
            services.AddKeyedSingleton<IMongoClientProvider>(connectionName, (sp, key) => sp.GetRequiredKeyedService<MongoClientProvider>(key));
            services.AddSingleton<IAsyncInitializer>(sp => sp.GetRequiredKeyedService<MongoClientProvider>(connectionName));

            return services;
        }
    }
}