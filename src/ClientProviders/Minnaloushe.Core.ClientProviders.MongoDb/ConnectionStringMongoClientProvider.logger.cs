using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Implementations;

internal static partial class ConnectionStringMongoClientProviderLogger
{
    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Debug,
        Message = "ConnectionStringMongoClientProvider constructed for connection '{ConnectionName}'")]
    public static partial void Constructed(
        this ILogger<ConnectionStringMongoClientProvider> logger,
        string connectionName);

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Trace,
        Message = "Acquiring IMongoClient lease for connection '{ConnectionName}'")]
    public static partial void AcquiringLease(
        this ILogger<ConnectionStringMongoClientProvider> logger,
        string connectionName);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Information,
        Message = "Initializing ConnectionStringMongoClientProvider for connection '{ConnectionName}'")]
    public static partial void Initializing(
        this ILogger<ConnectionStringMongoClientProvider> logger,
        string connectionName);

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Debug,
        Message = "Creating MongoDB client from connection string for connection '{ConnectionName}'")]
    public static partial void CreatingClient(
        this ILogger<ConnectionStringMongoClientProvider> logger,
        string connectionName);

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Information,
        Message = "ConnectionStringMongoClientProvider initialized for connection '{ConnectionName}'")]
    public static partial void Initialized(
        this ILogger<ConnectionStringMongoClientProvider> logger,
        string connectionName);

    [LoggerMessage(
        EventId = 4105,
        Level = LogLevel.Warning,
        Message = "ConnectionString must be provided for connection '{ConnectionName}' when using ConnectionStringMongoClientProvider.")]
    public static partial void MissingConnectionString(
        this ILogger<ConnectionStringMongoClientProvider> logger,
        string connectionName);
}

