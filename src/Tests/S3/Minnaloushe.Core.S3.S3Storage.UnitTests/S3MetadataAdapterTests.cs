using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Minnaloushe.Core.S3.S3Storage.MetadataAdapter;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;
using Moq;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.S3.S3Storage.UnitTests;

public class S3MetadataAdapterTests
{
    #region Test Data Classes

    private class TestMetadata
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    #endregion

    #region Fields

    private Fixture fixture = null!;
    private Mock<IS3StorageAdapter> adapterMock = null!;
    private Mock<ITempStreamFactory> tempStreamFactoryMock = null!;
    private ILogger<S3MetadataAdapter> logger = null!;
    private S3MetadataAdapter sut = null!;

    #endregion

    #region Setup

    [SetUp]
    public void Setup()
    {
        fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));

        adapterMock = new Mock<IS3StorageAdapter>(MockBehavior.Strict);
        tempStreamFactoryMock = new Mock<ITempStreamFactory>(MockBehavior.Strict);
        logger = NullLogger<S3MetadataAdapter>.Instance;

        tempStreamFactoryMock.Setup(x => x.Create())
            .Returns(() => new MemoryStream());

        sut = new S3MetadataAdapter(tempStreamFactoryMock.Object, adapterMock.Object, logger);
    }

    #endregion

    #region Helper Methods

    private static MemoryStream CreateTarStream(Dictionary<string, byte[]> entries)
    {
        var ms = new MemoryStream();
        using (var archive = new TarWriter(ms, new TarWriterOptions(CompressionType.None, true)))
        {
            foreach (var kv in entries)
            {
                var entryStream = new MemoryStream(kv.Value);
                archive.Write(kv.Key, entryStream);// AddEntry(kv.Key, entryStream, closeStream: false);
            }
        }
        ms.Position = 0;
        return ms;
    }

    #endregion

    #region GetMetadataStreamAsync Tests

    [Test]
    public async Task GetMetadataStreamAsync_WhenMetadataKeyExists_ThenRetrievesStream()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-metadata";
        var expectedData = Encoding.UTF8.GetBytes("test data");
        var tarStreamData = CreateTarStream(new Dictionary<string, byte[]> { { metadataKey, expectedData } }).ToArray();
        await using var outStream = new MemoryStream();

        adapterMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        // Act
        await sut.GetMetadataStreamAsync(blobKey, metadataKey, outStream);

        // Assert
        outStream.Position = 0;
        var result = new byte[outStream.Length];
        await outStream.ReadAsync(result);
        result.ShouldBe(expectedData);
        adapterMock.Verify(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default), Times.Once);
    }

    [Test]
    public async Task GetMetadataStreamAsync_WhenMetadataKeyNotFound_ThenReturnsEmptyStream()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "missing-key";
        var tarStreamData = CreateTarStream(new Dictionary<string, byte[]> { { "other-key", "data"u8.ToArray() } }).ToArray();
        await using var outStream = new MemoryStream();

        adapterMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        // Act & Assert

        await sut.GetMetadataStreamAsync(blobKey, metadataKey, outStream);

        outStream.Length.ShouldBe(0);
    }

    [Test]
    public async Task GetMetadataStreamAsync_WhenAdapterThrows_ThenPropagatesException()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-key";
        await using var outStream = new MemoryStream();

        adapterMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .ThrowsAsync(new InvalidOperationException("adapter error"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.GetMetadataStreamAsync(blobKey, metadataKey, outStream));
    }

    #endregion

    #region GetMetadataAsync Tests

    [Test]
    public async Task GetMetadataAsync_WhenMetadataExists_ThenReturnsDeserializedObject()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-metadata";
        var expectedMetadata = new TestMetadata
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Value = 42
        };
        var jsonData = JsonSerializer.SerializeToUtf8Bytes(expectedMetadata);
        var tarStreamData = CreateTarStream(new Dictionary<string, byte[]> { { metadataKey, jsonData } }).ToArray();

        adapterMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        // Act
        var result = await sut.GetMetadataAsync<TestMetadata>(blobKey, metadataKey);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(expectedMetadata.Id);
        result.Name.ShouldBe(expectedMetadata.Name);
        result.Value.ShouldBe(expectedMetadata.Value);
    }

    [Test]
    public async Task GetMetadataAsync_WhenDeserializationReturnsNull_ThenShouldReturnNull()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-metadata";
        var jsonData = "null"u8.ToArray();
        var tarStreamData = CreateTarStream(new Dictionary<string, byte[]> { { metadataKey, jsonData } }).ToArray();

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, _) =>
            {

            })
            .ReturnsAsync(false);

        // Act & Assert

        var result = await sut.GetMetadataAsync<TestMetadata>(blobKey, metadataKey);

        result.ShouldBeNull();
    }

    [Test]
    public async Task GetMetadataAsync_WhenInvalidJson_ThenThrowsJsonException()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-metadata";
        var invalidJsonData = "not valid json"u8.ToArray();
        var tarStreamData = CreateTarStream(new Dictionary<string, byte[]> { { metadataKey, invalidJsonData } }).ToArray();

        adapterMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        // Act & Assert
        await Should.ThrowAsync<JsonException>(
            async () => await sut.GetMetadataAsync<TestMetadata>(blobKey, metadataKey));
    }

    [Test]
    public async Task GetMetadataAsync_WhenMetadataKeyNotFound_ThenShouldReturnNull()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "missing-key";
        var tarStreamData = CreateTarStream(new Dictionary<string, byte[]> { { "other-key", "data"u8.ToArray() } }).ToArray();

        adapterMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        // Act & Assert
        var result = await sut.GetMetadataAsync<TestMetadata>(blobKey, metadataKey);

        result.ShouldBeNull();
    }

    #endregion

    #region ListMetadataKeysAsync Tests

    [Test]
    public async Task ListMetadataKeysAsync_WhenMultipleKeysExist_ThenReturnsAllKeys()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var keys = new Dictionary<string, byte[]>
        {
            { "key1", "data1"u8.ToArray() },
            { "key2", "data2"u8.ToArray() },
            { "key3", "data3"u8.ToArray() }
        };
        var tarStreamData = CreateTarStream(keys).ToArray();

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        // Act
        var result = await sut.ListMetadataKeysAsync(blobKey);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain("key1");
        result.ShouldContain("key2");
        result.ShouldContain("key3");
    }

    [Test]
    public async Task ListMetadataKeysAsync_WhenNoKeysExist_ThenReturnsEmptyCollection()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var tarStreamData = CreateTarStream([]).ToArray();

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        // Act
        var result = await sut.ListMetadataKeysAsync(blobKey);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Test]
    public async Task ListMetadataKeysAsync_WhenAdapterThrows_ThenPropagatesException()
    {
        // Arrange
        var blobKey = fixture.Create<string>();

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .ThrowsAsync(new InvalidOperationException("adapter error"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.ListMetadataKeysAsync(blobKey));
    }

    #endregion

    #region PutMetadataAsync Tests

    [Test]
    public async Task PutMetadataAsync_WhenArchiveDoesNotExist_ThenCreatesNewArchive()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "new-key";
        var data = Encoding.UTF8.GetBytes("test data");
        await using var dataStream = new MemoryStream(data);

        Stream? capturedStream = null;

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ReturnsAsync(false);

        adapterMock.Setup(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default))
            .Callback<string, Stream, IEnumerable<Models.BlobTag>?, string, CancellationToken>((_, s, _, _, _) =>
            {
                capturedStream = new MemoryStream();
                s.Position = 0;
                s.CopyTo(capturedStream);
                capturedStream.Position = 0;
            })
            .ReturnsAsync(new Models.BlobInfo { Key = $"{blobKey}/metadata", Size = 0, ETag = "etag" });

        // Act
        await sut.PutMetadataStreamAsync(blobKey, metadataKey, dataStream);

        // Assert
        capturedStream.ShouldNotBeNull();
        using var archive = TarArchive.OpenArchive(capturedStream);
        archive.Entries.ShouldContain(e => e.Key == metadataKey);

        var entry = archive.Entries.First(e => e.Key == metadataKey);
        await using var entryStream = await entry.OpenEntryStreamAsync();
        await using var resultStream = new MemoryStream();
        await entryStream.CopyToAsync(resultStream);
        resultStream.ToArray().ShouldBe(data);

        adapterMock.Verify(x => x.ExistsAsync($"{blobKey}/metadata", default), Times.Once);
        adapterMock.Verify(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default), Times.Once);
    }

    [Test]
    public async Task PutMetadataAsync_WhenArchiveExists_ThenUpdatesExistingArchive()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "new-key";
        var existingData = new Dictionary<string, byte[]>
        {
            { "existing-key", "existing data"u8.ToArray() }
        };
        var tarStreamData = CreateTarStream(existingData).ToArray();
        var newData = Encoding.UTF8.GetBytes("new data");
        await using var dataStream = new MemoryStream(newData);

        Stream? capturedStream = null;

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ReturnsAsync(true);

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        adapterMock.Setup(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default))
            .Callback<string, Stream, IEnumerable<Models.BlobTag>?, string, CancellationToken>((_, s, _, _, _) =>
            {
                capturedStream = new MemoryStream();
                s.Position = 0;
                s.CopyTo(capturedStream);
                capturedStream.Position = 0;
            })
            .ReturnsAsync(new Models.BlobInfo { Key = $"{blobKey}/metadata", Size = 0, ETag = "etag" });

        // Act
        await sut.PutMetadataStreamAsync(blobKey, metadataKey, dataStream);

        // Assert
        capturedStream.ShouldNotBeNull();
        using var archive = TarArchive.OpenArchive(capturedStream);
        archive.Entries.Count().ShouldBe(2);
        archive.Entries.ShouldContain(e => e.Key == "existing-key");
        archive.Entries.ShouldContain(e => e.Key == metadataKey);

        adapterMock.Verify(x => x.ExistsAsync($"{blobKey}/metadata", default), Times.Once);
        adapterMock.Verify(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default), Times.Once);
        adapterMock.Verify(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default), Times.Once);
    }

    [Test]
    public async Task PutMetadataAsync_WhenAdapterThrows_ThenPropagatesException()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-key";
        await using var dataStream = new MemoryStream(Encoding.UTF8.GetBytes("data"));

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ThrowsAsync(new InvalidOperationException("adapter error"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.PutMetadataAsync(blobKey, metadataKey, dataStream));
    }

    #endregion

    #region PutMetadataAsync<T> Tests

    [Test]
    public async Task PutMetadataAsyncGeneric_WhenArchiveDoesNotExist_ThenSerializesAndCreatesNewArchive()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-metadata";
        var metadata = new TestMetadata
        {
            Id = Guid.NewGuid(),
            Name = "Test Object",
            Value = 123
        };

        Stream? capturedStream = null;

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ReturnsAsync(false);

        adapterMock.Setup(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default))
            .Callback<string, Stream, IEnumerable<Models.BlobTag>?, string, CancellationToken>((_, s, _, _, _) =>
            {
                capturedStream = new MemoryStream();
                s.Position = 0;
                s.CopyTo(capturedStream);
                capturedStream.Position = 0;
            })
            .ReturnsAsync(new Models.BlobInfo { Key = $"{blobKey}/metadata", Size = 0, ETag = "etag" });

        // Act
        await sut.PutMetadataAsync(blobKey, metadataKey, metadata);

        // Assert
        capturedStream.ShouldNotBeNull();
        using var archive = TarArchive.OpenArchive(capturedStream);
        archive.Entries.ShouldContain(e => e.Key == metadataKey);

        var entry = archive.Entries.First(e => e.Key == metadataKey);
        await using var entryStream = await entry.OpenEntryStreamAsync();
        var deserializedMetadata = await JsonSerializer.DeserializeAsync<TestMetadata>(entryStream);

        deserializedMetadata.ShouldNotBeNull();
        deserializedMetadata.Id.ShouldBe(metadata.Id);
        deserializedMetadata.Name.ShouldBe(metadata.Name);
        deserializedMetadata.Value.ShouldBe(metadata.Value);

        adapterMock.Verify(x => x.ExistsAsync($"{blobKey}/metadata", default), Times.Once);
        adapterMock.Verify(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default), Times.Once);
    }

    [Test]
    public async Task PutMetadataAsyncGeneric_WhenArchiveExistsAndKeyDoesNotExist_ThenAppendsSerializedObject()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "new-metadata";
        var existingData = new Dictionary<string, byte[]>
        {
            { "existing-key", "existing data"u8.ToArray() }
        };
        var tarStreamData = CreateTarStream(existingData).ToArray();
        var metadata = new TestMetadata
        {
            Id = Guid.NewGuid(),
            Name = "New Object",
            Value = 456
        };

        Stream? capturedStream = null;

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ReturnsAsync(true);

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        adapterMock.Setup(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default))
            .Callback<string, Stream, IEnumerable<Models.BlobTag>?, string, CancellationToken>((_, s, _, _, _) =>
            {
                capturedStream = new MemoryStream();
                s.Position = 0;
                s.CopyTo(capturedStream);
                capturedStream.Position = 0;
            })
            .ReturnsAsync(new Models.BlobInfo { Key = $"{blobKey}/metadata", Size = 0, ETag = "etag" });

        // Act
        await sut.PutMetadataAsync(blobKey, metadataKey, metadata);

        // Assert
        capturedStream.ShouldNotBeNull();
        using var archive = TarArchive.OpenArchive(capturedStream);
        archive.Entries.Count().ShouldBe(2);
        archive.Entries.ShouldContain(e => e.Key == "existing-key");
        archive.Entries.ShouldContain(e => e.Key == metadataKey);

        var entry = archive.Entries.First(e => e.Key == metadataKey);
        await using var entryStream = await entry.OpenEntryStreamAsync();
        var deserializedMetadata = await JsonSerializer.DeserializeAsync<TestMetadata>(entryStream);

        deserializedMetadata.ShouldNotBeNull();
        deserializedMetadata.Id.ShouldBe(metadata.Id);
        deserializedMetadata.Name.ShouldBe(metadata.Name);
        deserializedMetadata.Value.ShouldBe(metadata.Value);

        adapterMock.Verify(x => x.ExistsAsync($"{blobKey}/metadata", default), Times.Once);
        adapterMock.Verify(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default), Times.Once);
        adapterMock.Verify(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default), Times.Once);
    }

    [Test]
    public async Task PutMetadataAsyncGeneric_WhenArchiveExistsAndKeyExists_ThenReplacesSerializedObject()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "existing-metadata";
        var oldMetadata = new TestMetadata
        {
            Id = Guid.NewGuid(),
            Name = "Old Object",
            Value = 100
        };
        var existingData = new Dictionary<string, byte[]>
        {
            { metadataKey, JsonSerializer.SerializeToUtf8Bytes(oldMetadata) },
            { "other-key", "other data"u8.ToArray() }
        };
        var tarStreamData = CreateTarStream(existingData).ToArray();
        var newMetadata = new TestMetadata
        {
            Id = Guid.NewGuid(),
            Name = "Updated Object",
            Value = 999
        };

        Stream? capturedStream = null;

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ReturnsAsync(true);

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        adapterMock.Setup(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default))
            .Callback<string, Stream, IEnumerable<Models.BlobTag>?, string, CancellationToken>((_, s, _, _, _) =>
            {
                capturedStream = new MemoryStream();
                s.Position = 0;
                s.CopyTo(capturedStream);
                capturedStream.Position = 0;
            })
            .ReturnsAsync(new Models.BlobInfo { Key = $"{blobKey}/metadata", Size = 0, ETag = "etag" });

        // Act
        await sut.PutMetadataAsync(blobKey, metadataKey, newMetadata);

        // Assert
        capturedStream.ShouldNotBeNull();
        using var archive = TarArchive.OpenArchive(capturedStream);
        archive.Entries.Count().ShouldBe(2);
        archive.Entries.ShouldContain(e => e.Key == metadataKey);
        archive.Entries.ShouldContain(e => e.Key == "other-key");

        var entry = archive.Entries.First(e => e.Key == metadataKey);
        await using var entryStream = await entry.OpenEntryStreamAsync();
        var deserializedMetadata = await JsonSerializer.DeserializeAsync<TestMetadata>(entryStream);

        deserializedMetadata.ShouldNotBeNull();
        deserializedMetadata.Id.ShouldBe(newMetadata.Id);
        deserializedMetadata.Name.ShouldBe(newMetadata.Name);
        deserializedMetadata.Value.ShouldBe(newMetadata.Value);
        deserializedMetadata.Id.ShouldNotBe(oldMetadata.Id);
        deserializedMetadata.Name.ShouldNotBe(oldMetadata.Name);
        deserializedMetadata.Value.ShouldNotBe(oldMetadata.Value);

        adapterMock.Verify(x => x.ExistsAsync($"{blobKey}/metadata", default), Times.Once);
        adapterMock.Verify(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default), Times.Once);
        adapterMock.Verify(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default), Times.Once);
    }

    [Test]
    public async Task PutMetadataAsyncGeneric_WhenAdapterThrows_ThenPropagatesException()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-key";
        var metadata = new TestMetadata
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Value = 42
        };

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ThrowsAsync(new InvalidOperationException("adapter error"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.PutMetadataAsync(blobKey, metadataKey, metadata));
    }

    #endregion

    #region DeleteMetadataAsync Tests

    [Test]
    public async Task DeleteMetadataAsync_WhenMetadataKeyExists_ThenRemovesKey()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "key-to-delete";
        var existingData = new Dictionary<string, byte[]>
        {
            { metadataKey, "data to delete"u8.ToArray() },
            { "other-key", "other data"u8.ToArray() }
        };
        var tarStreamData = CreateTarStream(existingData).ToArray();

        Stream? capturedStream = null;

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ReturnsAsync(true);

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        adapterMock.Setup(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default))
            .Callback<string, Stream, IEnumerable<Models.BlobTag>?, string, CancellationToken>((_, s, _, _, _) =>
            {
                capturedStream = new MemoryStream();
                s.Position = 0;
                s.CopyTo(capturedStream);
                capturedStream.Position = 0;
            })
            .ReturnsAsync(new Models.BlobInfo { Key = $"{blobKey}/metadata", Size = 0, ETag = "etag" });

        // Act
        await sut.DeleteMetadataAsync(blobKey, metadataKey);

        // Assert
        capturedStream.ShouldNotBeNull();
        using var archive = TarArchive.OpenArchive(capturedStream);
        archive.Entries.Count().ShouldBe(1);
        archive.Entries.ShouldNotContain(e => e.Key == metadataKey);
        archive.Entries.ShouldContain(e => e.Key == "other-key");

        adapterMock.Verify(x => x.ExistsAsync($"{blobKey}/metadata", default), Times.Once);
        adapterMock.Verify(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default), Times.Once);
        adapterMock.Verify(x => x.PutAsync($"{blobKey}/metadata", It.IsAny<Stream>(), null, It.IsAny<string>(), default), Times.Once);
    }

    [Test]
    public async Task DeleteMetadataAsync_WhenArchiveDoesNotExist_ThenThrowsS3MetadataKeyNotFoundException()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-key";

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ReturnsAsync(false);

        // Act & Assert
        var ex = await Should.ThrowAsync<S3MetadataNotFoundException>(
            async () => await sut.DeleteMetadataAsync(blobKey, metadataKey));

        ex.BlobKey.ShouldBe(blobKey);
    }

    [Test]
    public async Task DeleteMetadataAsync_WhenMetadataKeyDoesNotExist_ThenShouldNotThrow()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "missing-key";
        var existingData = new Dictionary<string, byte[]>
        {
            { "other-key", "other data"u8.ToArray() }
        };
        var tarStreamData = CreateTarStream(existingData).ToArray();

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ReturnsAsync(true);

        adapterMock.Setup(x => x.GetStreamAsync($"{blobKey}/metadata", It.IsAny<Stream>(), default))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(tarStreamData, 0, tarStreamData.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        // Act & Assert

        await sut.DeleteMetadataAsync(blobKey, metadataKey);
    }

    [Test]
    public async Task DeleteMetadataAsync_WhenAdapterThrows_ThenPropagatesException()
    {
        // Arrange
        var blobKey = fixture.Create<string>();
        var metadataKey = "test-key";

        adapterMock.Setup(x => x.ExistsAsync($"{blobKey}/metadata", default))
            .ThrowsAsync(new InvalidOperationException("adapter error"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.DeleteMetadataAsync(blobKey, metadataKey));
    }

    #endregion

    #region DeleteAllMetadataAsync Tests

    [Test]
    public async Task DeleteAllMetadataAsync_WhenCalled_ThenDeletesMetadataBlob()
    {
        // Arrange
        var blobKey = fixture.Create<string>();

        adapterMock.Setup(x => x.DeleteAsync($"{blobKey}/metadata", default))
            .Returns(Task.CompletedTask);

        // Act
        await sut.DeleteAllMetadataAsync(blobKey);

        // Assert
        adapterMock.Verify(x => x.DeleteAsync($"{blobKey}/metadata", default), Times.Once);
    }

    [Test]
    public async Task DeleteAllMetadataAsync_WhenAdapterThrows_ThenPropagatesException()
    {
        // Arrange
        var blobKey = fixture.Create<string>();

        adapterMock.Setup(x => x.DeleteAsync($"{blobKey}/metadata", default))
            .ThrowsAsync(new InvalidOperationException("adapter error"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.DeleteAllMetadataAsync(blobKey));
    }

    #endregion
}
