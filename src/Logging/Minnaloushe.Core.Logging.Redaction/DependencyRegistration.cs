using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Minnaloushe.Core.Logging.Redaction;

public static class DependencyRegistration
{
    public static IServiceCollection AddLogRedaction(this IServiceCollection services)
    {
        services.AddRedaction(options =>
        {
            // Map classification → redactor
            options.SetRedactor<StarRedactor>(SensitiveDataClassification.PrivateClassification);

            // Optional: fallback
            options.SetFallbackRedactor<ErasingRedactor>();
        });

        return services;
    }
}