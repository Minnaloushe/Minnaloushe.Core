using AwesomeAssertions;
using Minnaloushe.Core.ToolBox.MongoHacks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Minnaloushe.Core.Toolbox.MongoHacks.Tests;

public class MongoSerializerRebelTests
{
    // Your custom serializer example — just for demo (you can make it do whatever)
    private class RebelGuidSerializer(GuidRepresentation representation)
        : StructSerializerBase<Guid>, IRepresentationConfigurable<GuidSerializer>
    {
        private readonly GuidSerializer _internalInstance = new(representation);

        public override Guid Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var original = _internalInstance.Deserialize(context, args);
            // Proof we're being used: flip a bit or tag somehow (demo only)
            // ReSharper disable once StringLiteralTypo
            return Guid.Parse($"deadbeef-0000-0000-0000-{original.ToString("N")[20..]}");
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid value)
        {
            // Optional: alter during serialize too
            _internalInstance.Serialize(context, args, value);
        }

        public GuidSerializer WithRepresentation(BsonType representation)
        {
            return _internalInstance.WithRepresentation(representation);
        }

        public BsonType Representation => BsonType.Binary;

        IBsonSerializer IRepresentationConfigurable.WithRepresentation(BsonType representation)
        {
            return WithRepresentation(representation);
        }
    }

    private class GuidWrapper
    {
        public Guid Value { get; init; }
    }

    [Test]
    [Explicit("This test modifies global state and is meant for manual execution only.")]
    public void ForceReplaceSerializer_ShouldOverride_DefaultGuidSerializer()
    {
        // Arrange - simulate "someone else" already registered the default
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.CSharpLegacy));

        // Sanity check: confirm it's the legacy one
        var initial = BsonSerializer.LookupSerializer<Guid>();
        initial.Should().BeOfType<GuidSerializer>();
        ((GuidSerializer)initial).GuidRepresentation.Should().Be(GuidRepresentation.CSharpLegacy);

        // Act - apply the dirty hack
        MongoSerializerRebel.ForceReplaceSerializer(new RebelGuidSerializer(GuidRepresentation.JavaLegacy));

        // Assert - lookup now returns our rebel version
        var replaced = BsonSerializer.LookupSerializer<Guid>();
        replaced.Should().BeOfType<RebelGuidSerializer>();

        // Bonus: test actual serialization round-trip to prove it's hooked
        var originalGuid = Guid.NewGuid();
        var wrapper = new GuidWrapper { Value = originalGuid };
        var serialized = wrapper.ToJson();

        var deserialized = BsonSerializer.Deserialize<GuidWrapper>(serialized);
        var roundTrippedGuid = deserialized.Value;

        // Our RebelGuidSerializer modifies during deserialize → should NOT be identical
        roundTrippedGuid.Should().NotBe(originalGuid);
        roundTrippedGuid.ToString().Should().Contain("deadbeef"); // our marker

        // Cleanup (optional, for repeated test runs in same process)
        // You can't really unregister, but you could force-replace back if needed
    }
}