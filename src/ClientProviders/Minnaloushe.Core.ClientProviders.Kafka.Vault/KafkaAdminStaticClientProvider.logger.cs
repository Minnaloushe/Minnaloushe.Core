using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.ClientProviders.Kafka.Vault;

internal static partial class KafkaAdminStaticClientProviderLogger
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Initialized client for connection {ConnectionName}")]
    private static partial void InitializedClientCore(ILogger logger, string connectionName);

    internal static void InitializedClient(this ILogger<KafkaAdminStaticClientProvider> logger, string connectionName)
        => InitializedClientCore(logger, connectionName);
}
