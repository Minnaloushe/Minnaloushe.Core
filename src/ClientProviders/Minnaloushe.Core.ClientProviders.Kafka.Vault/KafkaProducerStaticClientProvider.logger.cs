using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.ClientProviders.Kafka.Vault;

internal static partial class KafkaProducerStaticClientProviderLogger
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Vault client is not initialized. Kafka producer client initialization is postponed.")]
    private static partial void VaultClientNotInitializedCore(ILogger logger);

    internal static void VaultClientNotInitialized(this ILogger<KafkaProducerStaticClientProvider> logger)
        => VaultClientNotInitializedCore(logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Initialized producer client for connection {ConnectionName}")]
    private static partial void InitializedProducerClientCore(ILogger logger, string connectionName);

    internal static void InitializedProducerClient(this ILogger<KafkaProducerStaticClientProvider> logger, string connectionName)
        => InitializedProducerClientCore(logger, connectionName);
}
