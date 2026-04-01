
using MongoDB.Bson.Serialization;
using System.Reflection;

namespace Minnaloushe.Core.ToolBox.MongoHacks;
/// <summary>
/// Provides methods for forcibly replacing the serializer for a specified type in the MongoDB serializer registry.
/// </summary>
/// <remarks>Don't use it. Created just for fun</remarks>
public static class MongoSerializerRebel
{
    public static void ForceReplaceSerializer<TValue>(IBsonSerializer<TValue> newSerializer)
    {
        // Step 1: Get the internal SerializerRegistry field (usually a BsonSerializerRegistry)
        var registryField = typeof(BsonSerializer)
            .GetField("__serializerRegistry", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Cannot find _registry field – driver version changed?");

        var registry = (IBsonSerializerRegistry)registryField.GetValue(null)!
                       ?? throw new InvalidOperationException("Registry is null");

        // Step 2: Get the internal _cache ConcurrentDictionary<Type, IBsonSerializer>
        var cacheField = registry.GetType()
            .GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(BsonSerializer)
                .GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Cannot find _cache field");

        var cache = cacheField.GetValue(registry
            ?? registryField.GetValue(null))
            ?? throw new InvalidOperationException("Cache is null");

        // Step 3: Use reflection to call .AddOrUpdate / replace (or direct set if possible)
        var typeKey = typeof(TValue);

        // Option A: Try to use AddOrUpdate if it's ConcurrentDictionary
        var addOrUpdateMethod = cache.GetType().GetMethod("AddOrUpdate",
            [typeof(Type), typeof(IBsonSerializer), typeof(Func<Type, IBsonSerializer, IBsonSerializer>)]);

        if (addOrUpdateMethod != null)
        {
            addOrUpdateMethod.Invoke(cache, [
                typeKey,
                newSerializer,
                (Func<Type, IBsonSerializer, IBsonSerializer>)((_, _) => newSerializer)
            ]);
        }
        else
        {
            // Option B: Brutal reflection set via indexer if exposed (rare)
            var indexer = cache.GetType().GetProperty("Item", [typeof(Type)]);
            if (indexer?.CanWrite == true)
            {
                indexer.SetValue(cache, newSerializer, [typeKey]);
            }
            else
            {
                // Option C: Nuke & re-add (most violent)
                var tryRemove = cache.GetType().GetMethod("TryRemove");
                tryRemove?.Invoke(cache, [typeKey, null]);

                var tryAdd = cache.GetType().GetMethod("TryAdd");
                tryAdd?.Invoke(cache, [typeKey, newSerializer]);
            }
        }

        Console.WriteLine($"Rebel yell: {typeof(TValue).Name} serializer forcibly replaced!");
    }
}