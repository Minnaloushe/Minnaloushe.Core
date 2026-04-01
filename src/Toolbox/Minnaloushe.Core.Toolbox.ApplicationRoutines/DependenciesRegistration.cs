using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.Toolbox.ApplicationRoutines.Options;

namespace Minnaloushe.Core.Toolbox.ApplicationRoutines;

public static class DependenciesRegistration
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddOptions<ApplicationOptions>()
            .BindConfiguration(ApplicationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
