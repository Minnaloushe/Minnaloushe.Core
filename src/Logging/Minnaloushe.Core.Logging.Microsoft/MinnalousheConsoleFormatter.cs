using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Minnaloushe.Core.Logging.Microsoft;

internal sealed class MinnalousheConsoleFormatter : ConsoleFormatter
{
    internal const string FormatterName = "minnaloushe-core";

    public MinnalousheConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options)
        : base(FormatterName) { }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var category = logEntry.Category ?? string.Empty;
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        if (logEntry.Exception is not null)
            message = $"{message} -> {logEntry.Exception.ToString().ReplaceLineEndings(" ")}";

        textWriter.WriteLine(
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.ffff} | " +
            $"{MapLogLevel(logEntry.LogLevel)} | " +
            $"{logEntry.EventId.Id} | " +
            $"{category} | " +
            $"{category.GetHashCode():x8} | " +
            $"{Thread.CurrentThread.ManagedThreadId} | " +
            $"{Task.CurrentId ?? 0} | " +
            $"{GetCorrelationId(scopeProvider)} | " +
            $"{message}");
    }

    private static string MapLogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "FATAL",
        _ => "NONE"
    };

    // Walks the MEL scope stack looking for a CorrelationId key — equivalent to
    // NLog's ${mdlc:CorrelationId:whenEmpty=-} which reads from the same scope chain.
    private static string GetCorrelationId(IExternalScopeProvider? scopeProvider)
    {
        string? result = null;

        scopeProvider?.ForEachScope(
            (scope, _) =>
            {
                if (result is not null || scope is not IEnumerable<KeyValuePair<string, object?>> properties)
                    return;

                foreach (var kvp in properties)
                {
                    if (string.Equals(kvp.Key, "CorrelationId", StringComparison.Ordinal))
                    {
                        result = kvp.Value?.ToString();
                        break;
                    }
                }
            },
            (object?)null);

        return result ?? "-";
    }
}


