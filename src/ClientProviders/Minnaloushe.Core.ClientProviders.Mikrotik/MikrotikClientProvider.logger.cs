using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.ClientProvider.Mikrotik;

internal static partial class MikrotikClientProviderLogger
{
    [LoggerMessage(LogLevel.Warning, "Retry attempt {AttemptNumber} after {DelayMs}ms due to: {ExceptionMessage}")]
    internal static partial void LogRetryAttempt(this ILogger logger, int AttemptNumber, double DelayMs, string? ExceptionMessage);
    [LoggerMessage(LogLevel.Information, "Reconnecting to router...")]
    internal static partial void LogReconnectingToRouter(this ILogger logger);

}