namespace Minnaloushe.Core.MessageQueues.Abstractions;

public record MessageEnvelop<TMessage>(TMessage Message, string? Key = null, IReadOnlyDictionary<string, string>? Headers = null);