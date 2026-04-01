using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.ClientProviders.Kafka.Vault;

internal static partial class KafkaConsumerStaticClientProviderLogger
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Vault client is not initialized. Kafka client initialization is postponed.")]
    private static partial void VaultClientNotInitializedCore(ILogger logger);

    internal static void VaultClientNotInitialized(this ILogger<KafkaConsumerStaticClientProvider> logger)
        => VaultClientNotInitializedCore(logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Initialized client for connection {ConnectionName}")]
    private static partial void InitializedClientCore(ILogger logger, string connectionName);

    internal static void InitializedClient(this ILogger<KafkaConsumerStaticClientProvider> logger, string connectionName)
        => InitializedClientCore(logger, connectionName);
}
