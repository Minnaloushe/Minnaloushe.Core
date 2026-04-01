using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

namespace Minnaloushe.Core.ClientProviders.Telegram;

public static class DependencyRegistration
{
    public static KeyedSingletonBuilder AddKeyedTelegramClientProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.RegisterKeyedClientProvider<
            TelegramOptions,
            ITelegramClientProvider,
            TelegramClientProvider,
            ITelegramBotClientFactory,
            TelegramBotClientFactory>(
            configuration,
            sectionName: TelegramOptions.SectionName,
            providerFactory: (sp, _, factory, resolvedOptions) =>
                new TelegramClientProvider(
                    resolvedOptions,
                    factory,
                    sp.GetRequiredService<ILogger<TelegramClientProvider>>()
                    )
            );
    }

    public static IServiceCollection AddTelegramClientProvider(this IServiceCollection services)
    {
        return services.AddClientProvider<
            ITelegramClientProvider,
            TelegramClientProvider,
            TelegramOptions>(TelegramOptions.SectionName);
    }
}