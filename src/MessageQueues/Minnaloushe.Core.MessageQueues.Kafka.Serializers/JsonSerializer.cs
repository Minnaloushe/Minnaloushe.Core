using Confluent.Kafka;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Kafka.Serializers;

public class JsonSerializer<T>(JsonSerializerOptions options) : ISerializer<T>
{
    public byte[] Serialize(T data, SerializationContext context)
    {
        return data == null ? null! : JsonSerializer.SerializeToUtf8Bytes(data, options);
    }
}