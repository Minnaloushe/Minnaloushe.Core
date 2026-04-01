using AutoFixture;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.CompressedStorageAdapter;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Minnaloushe.Core.S3.S3Storage.Models;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;
using Moq;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Shouldly;
using System.Text;

namespace Minnaloushe.Core.S3.S3Storage.UnitTests;

public class S3CompressedStorageAdapterTests
{
    private class TestMetadata
    {
        public Guid Id { get; set; }
        public string? FileName { get; set; }
        public List<KeyValuePair<string, string>>? FileMetadata { get; set; }
    }

    private Fixture fixture = null!;
    private Mock<IS3StorageAdapter> adapterMock = null!;
    private Mock<ILogger<S3CompressedStorageAdapter>> loggerMock = null!;
    private S3CompressedStorageAdapter sut = null!;
    private readonly Mock<ITempStreamFactory> tempStreamFactoryMock = new();

    [SetUp]
    public void Setup()
    {
        fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));

        tempStreamFactoryMock.Reset();
        tempStreamFactoryMock.Setup(x => x.Create())
            .Returns(() => new MemoryStream());

        adapterMock = new Mock<IS3StorageAdapter>(MockBehavior.Strict);
        loggerMock = new Mock<ILogger<S3CompressedStorageAdapter>>(MockBehavior.Loose);

        sut = new S3CompressedStorageAdapter(adapterMock.Object, tempStreamFactoryMock.Object, loggerMock.Object);
    }

    private static MemoryStream CreateZipStream(Dictionary<string, byte[]> entries)
    {
        var ms = new MemoryStream();
        using (var writer = new ZipWriter(ms, new ZipWriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }))
        {
            foreach (var kv in entries)
            {
                using var entryStream = new MemoryStream(kv.Value);
                writer.Write(kv.Key, entryStream);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private class TrackableStream(byte[] buffer) : MemoryStream(buffer)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return base.DisposeAsync();
        }
    }

    [Test]
    public async Task GetUncompressedAsync_WhenAdapterGetStreamThrows_ThrowsS3StorageException()
    {
        var key = fixture.Create<string>();
        var target = new MemoryStream();

        adapterMock
            .Setup(x => x.GetStreamAsync(key, It.IsAny<Stream>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        adapterMock.Setup(x => x.GetTagsAsync(key)).ReturnsAsync([]);

        var ex = await Should.ThrowAsync<S3StorageException>(async () => await sut.GetUncompressedAsync(key, target));
        ex.Message.ShouldContain(key);

        adapterMock.Verify(x => x.GetStreamAsync(key, It.IsAny<Stream>()), Times.Once);
    }

    [Test]
    public async Task GetUncompressedAsync_WhenNotMarkedCompressed_ReturnsOriginalStream()
    {
        var key = fixture.Create<string>();
        var data = Encoding.UTF8.GetBytes("plain");
        await using var ms = new TrackableStream(data);


        adapterMock.Setup(x => x.GetStreamAsync(key, ms)).Returns(async () => ms);
        adapterMock.Setup(x => x.GetTagsAsync(key)).ReturnsAsync([]);

        await sut.GetUncompressedAsync(key, ms);

        // Should return same stream instance
        ms.Disposed.ShouldBeFalse();

        adapterMock.Verify(x => x.GetStreamAsync(key, ms), Times.Once);
        adapterMock.Verify(x => x.GetTagsAsync(key), Times.Once);
    }

    [Test]
    public async Task GetUncompressedAsync_WhenCompressedWithSingleEntry_ReturnsEntryStreamAndDisposesOriginal()
    {
        var key = fixture.Create<string>();
        var payload = "hello-world"u8.ToArray();
        // Prepare a zip payload and ensure the temp stream created inside the SUT is trackable
        await using var zip = CreateZipStream(new Dictionary<string, byte[]> { { "content", payload } });
        var zipBytes = zip.ToArray();

        var tempTrackable = new TrackableStream(new byte[8192]);
        tempStreamFactoryMock.Setup(x => x.Create()).Returns(() => tempTrackable);

        await using var result = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);

        // When adapter is asked to fill the temp stream, write the zip bytes into it
        adapterMock.Setup(x => x.GetStreamAsync(key, It.IsAny<Stream>()))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(zipBytes, 0, zipBytes.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        adapterMock.Setup(x => x.GetTagsAsync(key)).ReturnsAsync([new BlobTag("x-compressed", "true")]);

        await sut.GetUncompressedAsync(key, result);

        // Ensure result contains the uncompressed bytes
        result.Position = 0;
        using var sr = new StreamReader(result, Encoding.UTF8, leaveOpen: false);
        var text = await sr.ReadToEndAsync();
        text.ShouldBe("hello-world");

        // temp stream created inside SUT should have been disposed
        tempTrackable.Disposed.ShouldBeTrue();

        adapterMock.Verify(x => x.GetStreamAsync(key, It.IsAny<Stream>()), Times.Once);
        adapterMock.Verify(x => x.GetTagsAsync(key), Times.Once);
    }

    [Test]
    public async Task GetUncompressedAsync_WhenCompressedWithMultipleEntries_ReturnsOriginalStream()
    {
        var key = fixture.Create<string>();
        var entryA = "a"u8.ToArray();
        var entryB = "b"u8.ToArray();
        await using var zip = CreateZipStream(new Dictionary<string, byte[]> { { "a", entryA }, { "b", entryB } });
        var zipBytes = zip.ToArray();

        //var tempTrackable = new MemoryStream();
        var tempTrackable = new TrackableStream(new byte[zip.Length]);
        tempStreamFactoryMock.Setup(x => x.Create()).Returns(() => tempTrackable);

        await using var result = new MemoryStream();

        adapterMock.Setup(x => x.GetStreamAsync(key, It.IsAny<Stream>()))
            .Callback<string, Stream, CancellationToken>((k, s, _) =>
            {
                s.Write(zipBytes, 0, zipBytes.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        adapterMock.Setup(x => x.GetTagsAsync(key)).ReturnsAsync([new BlobTag("x-compressed", "true")]);

        await sut.GetUncompressedAsync(key, result);

        // No "content" entry -> result should contain unprocessed zip bytes
        result.Length.ShouldBe(zipBytes.Length);
        tempTrackable.Disposed.ShouldBeTrue();

        adapterMock.Verify(x => x.GetStreamAsync(key, It.IsAny<Stream>()), Times.Once);
        adapterMock.Verify(x => x.GetTagsAsync(key), Times.Once);
    }

    [Test]
    public async Task GetUncompressedAsync_WhenCompressedButArchiveInvalid_ThrowsS3StorageException()
    {
        // Arrange
        var key = fixture.Create<string>();
        var notZip = "not-a-zip"u8.ToArray();
        var tempTrackable = new TrackableStream(new byte[8192]);
        tempStreamFactoryMock.Setup(x => x.Create()).Returns(() => tempTrackable);
        using var result = new MemoryStream();

        adapterMock.Setup(x => x.GetStreamAsync(key, It.IsAny<Stream>()))
            .Callback<string, Stream, CancellationToken>((_, s, _) =>
            {
                s.Write(notZip, 0, notZip.Length);
                s.Position = 0;
            })
            .Returns(Task.CompletedTask);

        adapterMock.Setup(x => x.GetTagsAsync(key)).ReturnsAsync([new BlobTag("x-compressed", "true")]);

        // Act & Assert
        var ex = await Should.ThrowAsync<S3StorageException>(async () => await sut.GetUncompressedAsync(key, result));
        ex.Message.ShouldContain(key);

        adapterMock.Verify(x => x.GetStreamAsync(key, It.IsAny<Stream>()), Times.Once);
        adapterMock.Verify(x => x.GetTagsAsync(key), Times.Once);
    }

    [Test]
    public async Task PutCompressedAsync_CompressesDataAndAppendsCompressedTag()
    {
        var key = fixture.Create<string>();
        var original = "original-payload"u8.ToArray();
        var inputStream = new MemoryStream(original);

        byte[]? capturedUploadBytes = null;
        IEnumerable<BlobTag>? capturedTags = null;

        var expectedBlob = new BlobInfo { Key = key, Size = (ulong)original.Length, ETag = "\"etag\"" };

        adapterMock
            .Setup(x => x.PutAsync(It.Is<string>(s => s == key),
                                   It.IsAny<Stream>(),
                                   It.IsAny<IEnumerable<BlobTag>>(),
                                   It.Is<string>(s => s == "application/zip")))
            .Callback<string, Stream, IEnumerable<BlobTag>?, string, CancellationToken>((_, s, tags, _, _) =>
            {
                // read the stream into byte[] for verification
                using var ms = new MemoryStream();
                s.Position = 0;
                s.CopyTo(ms);
                capturedUploadBytes = ms.ToArray();
                capturedTags = tags;
            })
            .ReturnsAsync(expectedBlob);

        var result = await sut.PutCompressedAsync(key, inputStream,
        [
            new BlobTag("x-other", "v1"), new BlobTag("x-compressed", "should-be-ignored")
        ]);

        // Ensure adapter returned blob forwarded
        result.ShouldBe(expectedBlob);

        // Verify uploaded content is a zip with a single entry "content" and original payload inside
        capturedUploadBytes.ShouldNotBeNull();
        await using var uploadedStream = new MemoryStream(capturedUploadBytes);
        using var zip = SharpCompress.Archives.Zip.ZipArchive.OpenArchive(uploadedStream, new ReaderOptions() { LeaveStreamOpen = false });
        zip.Entries.Count().ShouldBe(1);
        var entry = zip.Entries.First(f => f.Key == "content");

        await using var es = await entry.OpenEntryStreamAsync();
        await using var outMs = new MemoryStream();
        await es.CopyToAsync(outMs);
        outMs.ToArray().ShouldBe(original);

        // Verify tags include compressed tag exactly once and keep other tags except any incoming compressed tag
        capturedTags.ShouldNotBeNull();
        var tagsList = capturedTags!.ToList();
        tagsList.Count(t => t.Key == "x-compressed").ShouldBe(1);
        tagsList.ShouldContain(t => t.Key == "x-other" && t.Value == "v1");

        adapterMock.Verify(x => x.PutAsync(key, It.IsAny<Stream>(), It.IsAny<IEnumerable<BlobTag>>(), "application/zip"), Times.Once);
    }

    [Test]
    public async Task PutCompressedAsync_WhenAdapterThrows_ThrowsS3StorageException()
    {
        var key = fixture.Create<string>();
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("payload"));

        adapterMock
            .Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<IEnumerable<BlobTag>>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("upstream"));

        var ex = await Should.ThrowAsync<S3StorageException>(async () => await sut.PutCompressedAsync(key, input));
        ex.Message.ShouldContain(key);

        adapterMock.Verify(x => x.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<IEnumerable<BlobTag>>(), It.IsAny<string>()), Times.Once);
    }
}