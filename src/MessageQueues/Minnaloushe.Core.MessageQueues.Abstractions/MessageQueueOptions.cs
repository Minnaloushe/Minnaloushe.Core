using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.Toolbox.RetryRoutines.Options;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Abstractions;

public record MessageQueueOptions
{
    public string ConnectionName { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public ushort Port { get; init; } = 0;
    public string Type { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;
    public string ServiceKey { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public int Parallelism { get; init; } = 1;
    public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public RetryPolicyOptions RetryPolicy { get; init; } = new();
    public ErrorHandlingStrategy ErrorHandling { get; init; } = ErrorHandlingStrategy.NackAndDiscard;

    /// <summary>
    /// Provider-specific parameters. This property is settable to allow merging of
    /// connection-level and consumer-level parameters during options configuration.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; set; } = new Dictionary<string, JsonElement>();

    public TimeSpan ConsumerLoopDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ConsumerErrorDelay { get; init; } = TimeSpan.FromSeconds(5);
}