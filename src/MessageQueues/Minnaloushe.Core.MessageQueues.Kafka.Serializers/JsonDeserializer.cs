using Confluent.Kafka;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Kafka.Serializers;

public class JsonDeserializer<T>(JsonSerializerOptions options) : IDeserializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        return isNull ? default! : JsonSerializer.Deserialize<T>(data, options)!;
    }
}