using Minnaloushe.Core.Toolbox.StringExtensions;

namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Common header keys used for dead letter messages.
/// </summary>
public static class DeadLetterHeaders
{
    /// <summary>Key prefix for all dead letter headers.</summary>
    public const string Prefix = "dlt-";

    /// <summary>The original topic or queue name.</summary>
    public const string OriginalTopic = $"{Prefix}original-topic";

    /// <summary>The exception type that caused the failure.</summary>
    public const string ExceptionType = $"{Prefix}exception-type";

    /// <summary>The exception message.</summary>
    public const string ExceptionMessage = $"{Prefix}exception-message";

    /// <summary>The exception stack trace.</summary>
    public const string ExceptionStackTrace = $"{Prefix}exception-stacktrace";

    /// <summary>Timestamp when the message failed.</summary>
    public const string FailureTimestamp = $"{Prefix}failure-timestamp";

    /// <summary>The consumer name that failed to process the message.</summary>
    public const string ConsumerName = $"{Prefix}consumer-name";

    /// <summary>Number of retry attempts.</summary>
    public const string RetryCount = $"{Prefix}retry-count";

    /// <summary>Service key associated with the message.</summary>
    public const string ServiceKey = $"{Prefix}service-key";

    /// <summary>
    /// Creates a dictionary of dead letter headers from failed message details.
    /// Includes original message headers (if present) plus DLT-specific metadata.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CreateHeaders(
        FailedMessageDetails details,
        string? consumerName = null,
        int retryCount = 0)
    {
        var headers = new Dictionary<string, string>();

        // First, copy original headers (if any) so they're preserved
        if (details.OriginalHeaders is not null)
        {
            foreach (var (key, value) in details.OriginalHeaders)
            {
                headers[key] = value;
            }
        }

        // Add DLT-specific headers (these will override any original headers with the same key)
        headers[OriginalTopic] = details.Topic;
        headers[FailureTimestamp] = details.Timestamp.ToString("O");

        if (details.Exception is not null)
        {
            headers[ExceptionType] = details.Exception.GetType().FullName ?? details.Exception.GetType().Name;
            headers[ExceptionMessage] = details.Exception.Message;

            if (details.Exception.StackTrace is not null)
            {
                // Truncate stack trace if too long (max 8KB for header safety)
                var stackTrace = details.Exception.StackTrace;
                if (stackTrace.Length > 8192)
                {
                    stackTrace = stackTrace[..8192] + "...[truncated]";
                }
                headers[ExceptionStackTrace] = stackTrace;
            }
        }

        if (!string.IsNullOrEmpty(consumerName))
        {
            headers[ConsumerName] = consumerName;
        }

        if (retryCount > 0)
        {
            headers[RetryCount] = retryCount.ToString();
        }

        if (details.ServiceKey.IsNotNullOrWhiteSpace())
        {
            headers[ServiceKey] = details.ServiceKey;
        }

        return headers;
    }
}
