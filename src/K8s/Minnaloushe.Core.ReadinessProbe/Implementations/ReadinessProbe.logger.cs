using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.ReadinessProbe.Implementations;

internal static partial class ReadinessProbeLogger
{
    [LoggerMessage(LogLevel.Information, "{Class} reported {State} state")]
    public static partial void LogClassReportedStateState(this ILogger logger, string Class, HealthStatus State);
}