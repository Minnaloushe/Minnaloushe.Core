using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minnaloushe.Core.ApiClient.Interfaces;

namespace Minnaloushe.Core.ApiClient;

public static class ClientRegistration
{
    public static IServiceCollection RegisterClient<TInterface, TImplementation, TSettings>(
        this IServiceCollection services, IConfiguration configuration, string clientId,
        Func<IHttpClientAdapter, TImplementation> initiatorFunc)
        where TImplementation : ApiClientBase, TInterface
        where TInterface : class
        where TSettings : ApiClientSettings
    {
        services.Configure<TSettings>(
            configuration.GetSection(typeof(TSettings).Name));

        var settings = configuration.GetSection(typeof(TSettings).Name).Get<TSettings>();

        switch (settings)
        {
            case null:
                throw new ArgumentException($"Unable to get settings {typeof(TSettings).Name}");
        }

        services.AddSingleton<TInterface>(sp => sp.GetRequiredService<TImplementation>());

        services.AddTransient<TImplementation>(sp =>
        {
            var adapter = new HttpClientAdapter(sp.GetService<IHttpClientFactory>()!
                .CreateClient(clientId));
            return initiatorFunc(adapter);
        });

        services.AddHttpClient(clientId, (provider, client) =>
        {
            client.BaseAddress = new Uri(settings.BaseAddress);
            client.Timeout = settings.Timeout;
        });

        return services;
    }

    public static IHostBuilder RegisterClient<TInterface, TImplementation, TSettings>(this IHostBuilder builder,
        string clientId)
        where TImplementation : ApiClientBase, TInterface
        where TInterface : class
        where TSettings : ApiClientSettings
    {
        builder.ConfigureServices((context, services) =>
        {
            services.Configure<TSettings>(
                context.Configuration.GetSection(typeof(TSettings).Name));

            var settings = context.Configuration.GetSection(typeof(TSettings).Name).Get<TSettings>();

            switch (settings)
            {
                case null:
                    throw new ArgumentException($"Failed to read settings section {typeof(TSettings).Name}");
            }

            services.AddSingleton<TInterface>(sp => sp.GetRequiredService<TImplementation>());

            services.AddTransient<TImplementation>(sp =>
            {
                var adapter = new HttpClientAdapter(sp.GetService<IHttpClientFactory>()!
                    .CreateClient(clientId));

                return ActivatorUtilities.CreateInstance<TImplementation>(sp, adapter);
            });

            services.AddHttpClient(clientId, (p, client) =>
            {
                client.BaseAddress = new Uri(settings.BaseAddress);
                client.Timeout = settings.Timeout;
            });
        });

        return builder;
    }
}