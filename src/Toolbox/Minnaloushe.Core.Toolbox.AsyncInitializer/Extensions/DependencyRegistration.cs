using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ReadinessProbe;
using Minnaloushe.Core.Toolbox.AsyncInitializer.KeyedInitializer;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Options;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Services;

namespace Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;

public static class DependencyRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureAsyncInitializers()
        {
            services.AddOptions<AsyncInitializerOptions>()
                .BindConfiguration(AsyncInitializerOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingletonAsReadinessProbe<AsyncInitializerService>();

            services.AddSingleton<KeyedInitializerRegistry>();
            services.AddSingleton<IKeyedInitializerRegistry>(sp => sp.GetRequiredService<KeyedInitializerRegistry>());

            return services;
        }

        public IServiceCollection AddAsyncInitializer<TInitializer>()
            where TInitializer : class, IAsyncInitializer
        {
            services.AddSingleton<TInitializer>();
            services.AddSingleton<IAsyncInitializer>(sp => sp.GetRequiredService<TInitializer>());
            return services;
        }

        public IServiceCollection AddAsyncInitializer<TInitializer>(Func<IServiceProvider, TInitializer> factory)
            where TInitializer : class, IAsyncInitializer
        {
            services.AddSingleton(factory);
            services.AddSingleton<IAsyncInitializer>(sp => sp.GetRequiredService<TInitializer>());
            return services;
        }

        public IServiceCollection AddKeyedAsyncInitializer<TInitializer>(object key)
            where TInitializer : class, IAsyncInitializer
        {
            services.AddSingleton(new AsyncKeyedServiceDescriptor(typeof(TInitializer), key));
            return services;
        }
    }
}
