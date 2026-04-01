using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Minnaloushe.Core.Toolbox.JsonConfiguration;

public static class DependenciesRegistration
{
    public static IServiceCollection AddJsonConfiguration(this IServiceCollection services, Action<JsonSerializerOptions>? runtimeOptions = null)
    {
        if (services.Any(s => s.ServiceType == typeof(JsonSerializerSettings) && !s.IsKeyedService))
        {
            return services;
        }

        services.AddOptions<JsonSerializerSettings>()
            .BindConfiguration(JsonSerializerSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<JsonSerializerOptions>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<JsonSerializerSettings>>();

            return JsonSerializerOptionsFactory.Create(
                settings.Value,
                configureRuntime: options => runtimeOptions?.Invoke(options)
                );
        });
        // Placeholder for future JSON configuration services
        return services;
    }
}
