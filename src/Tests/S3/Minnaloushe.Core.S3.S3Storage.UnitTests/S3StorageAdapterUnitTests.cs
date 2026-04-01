using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Tags;
using Minio.Exceptions;
using Minnaloushe.Core.ClientProviders.Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Moq;
using Shouldly;

namespace Minnaloushe.Core.S3.S3Storage.UnitTests;

[TestFixture]
public class S3StorageAdapterUnitTests
{
    private Fixture _fixture = null!;
    private Mock<IMinioClient> _minioMock = null!;
    private readonly Mock<IMinioClientProvider> _clientProvider = new();
    private ILogger<S3StorageAdapter> _nullLogger = null!;
    private S3StorageOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = new Fixture();
        _minioMock = new Mock<IMinioClient>(MockBehavior.Strict);

        _clientProvider.SetupGet(x => x.Client)
            .Returns(_minioMock.Object);

        _nullLogger = NullLogger<S3StorageAdapter>.Instance;
        var opts = new S3StorageOptions
        {
            ServiceUrl = "https://s3.test",
            AccessKey = "AK",
            SecretKey = "SK",
            BucketName = "test-bucket",
            SyncRules = false,
            LifecycleRules = []
        };
        _options = opts;
    }

    [TearDown]
    public void TearDown()
    {
        _minioMock.VerifyAll();
    }

    [Test]
    public async Task GetTagsAsync_ReturnsTags_WhenMinioReturnsTags()
    {
        // Arrange
        var key = _fixture.Create<string>();
        var minioTags = new Tagging(new Dictionary<string, string>()
        {
            { "k1", "v1" },
            { "k2", "v2" }
        }, false);

        _minioMock
            .Setup(x => x.GetObjectTagsAsync(It.IsAny<GetObjectTagsArgs>(), CancellationToken.None))
            .ReturnsAsync(minioTags);

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act
        var tags = await adapter.GetTagsAsync(key);

        // Assert
        tags.Count.ShouldBe(2);
        tags.ShouldContain(t => t.Key == "k1" && t.Value == "v1");
        tags.ShouldContain(t => t.Key == "k2" && t.Value == "v2");
    }

    [Test]
    public void GetTagsAsync_ThrowsS3StorageException_OnMinioException()
    {
        // Arrange
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(x => x.GetObjectTagsAsync(It.IsAny<GetObjectTagsArgs>(), CancellationToken.None))
            .ThrowsAsync(new MinioException("fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act & Assert
        Should.ThrowAsync<S3StorageException>(() => adapter.GetTagsAsync(key));
    }

    [Test]
    public async Task ExistsAsync_ReturnsFalse_WhenObjectNotFoundException()
    {
        // Arrange
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new ObjectNotFoundException("not found"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act
        var exists = await adapter.ExistsAsync(key);

        // Assert
        exists.ShouldBeFalse();
    }

    [Test]
    public async Task ExistsAsync_ReturnsTrue_WhenStatSucceeds()
    {
        // Arrange
        var key = _fixture.Create<string>();
        // StatObjectAsync returns a StatObjectResponse; the adapter only awaits success => we can return a dummy object.
        _minioMock
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ReturnsAsync(TestHelpers.GetObjectStat(key)); // type from Minio SDK

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act
        var exists = await adapter.ExistsAsync(key);

        // Assert
        exists.ShouldBeTrue();
    }

    [Test]
    public void ExistsAsync_ThrowsS3StorageException_OnMinioException_OtherThanNotFound()
    {
        // Arrange
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new MinioException("boom"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act & Assert
        Should.ThrowAsync<S3StorageException>(() => adapter.ExistsAsync(key));
    }

    [Test]
    public async Task GetBlobInfoAsync_ReturnsBlobInfo_OnSuccess()
    {
        // Arrange
        var key = _fixture.Create<string>();

        var stat = TestHelpers.GetObjectStat(key, "\"the-etag\"", 42);

        _minioMock
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ReturnsAsync(stat);

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act
        var info = await adapter.GetBlobInfoAsync(key);

        // Assert
        info.Key.ShouldBe(key);
        info.ETag.ShouldBe("the-etag"); // trimmed quotes
        info.Size.ShouldBe((ulong)stat.Size);
        info.LastModified.ShouldNotBeNull();
    }

    [Test]
    public void GetBlobInfoAsync_ThrowsS3StorageException_WhenObjectNotFound()
    {
        // Arrange
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new ObjectNotFoundException("no"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act & Assert
        Should.ThrowAsync<S3StorageException>(() => adapter.GetBlobInfoAsync(key));
    }

    [Test]
    public async Task ListAsync_ReturnsItems_FromListObjectsEnumAsync()
    {
        // Arrange
        var items = new[]
        {
            new Item { Key = "a.txt", Size = 10, ETag = "\"e1\""},
            new Item { Key = "b.txt", Size = 20, ETag = "\"e2\""}
        };

        _minioMock
            .Setup(x => x.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), CancellationToken.None))
            .Returns((ListObjectsArgs args, CancellationToken ct) => TestHelpers.ToAsync(items));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act
        var result = await adapter.ListAsync();

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(b => b.Key == "a.txt");
        result.ShouldContain(b => b.Key == "b.txt");
    }

    [Test]
    public async Task RenameAsync_Throws_WhenSourceMissing()
    {
        // Arrange
        var src = "src.txt";
        var dest = "dest.txt";

        // ExistsAsync uses StatObjectAsync; configure source as not found
        _minioMock
            .Setup(x => x.StatObjectAsync(It.Is<StatObjectArgs>(a => a != null), CancellationToken.None))
            .ThrowsAsync(new ObjectNotFoundException("no"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act & Assert
        await Should.ThrowAsync<S3StorageException>(() => adapter.RenameAsync(src, dest));
    }

    [Test]
    public async Task RenameAsync_Throws_WhenDestinationExists_AndOverwriteFalse()
    {
        // Arrange
        var src = "src.txt";
        var dest = "dest.txt";

        // First call to StatObjectAsync (for src) -> success
        var stat = TestHelpers.GetObjectStat(dest, "\"e\"", 1);

        var seq = new MockSequence();
        _minioMock.InSequence(seq)
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ReturnsAsync(stat); // source exists

        // Second call to StatObjectAsync (for dest) -> success (destination exists)
        _minioMock.InSequence(seq)
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ReturnsAsync(stat); // dest exists

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act & Assert
        await Should.ThrowAsync<S3StorageException>(() => adapter.RenameAsync(src, dest, overwrite: false));
    }

    [Test]
    public async Task RenameAsync_PerformsCopyDeleteAndReturnsInfo_WhenDestinationDoesNotExistOrOverwrite()
    {
        // Arrange
        var src = "src.txt";
        var dest = "dest.txt";

        var stat = TestHelpers.GetObjectStat(dest, "\"e\"");

        var seq = new MockSequence();

        // 1. ExistsAsync(source) -> Stat succeeds
        _minioMock.InSequence(seq)
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ReturnsAsync(stat);

        // 2. ExistsAsync(dest) -> throw ObjectNotFoundException to indicate does not exist
        _minioMock.InSequence(seq)
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new ObjectNotFoundException("not found"));

        // 3. CopyObjectAsync should be called
        _minioMock.InSequence(seq)
            .Setup(x => x.CopyObjectAsync(It.IsAny<CopyObjectArgs>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // 4. RemoveObjectAsync (delete original) should be called
        _minioMock.InSequence(seq)
            .Setup(x => x.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // 5. GetBlobInfoAsync (StatObjectAsync for dest) should return stat for final info
        _minioMock.InSequence(seq)
            .Setup(x => x.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ReturnsAsync(stat);

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // Act
        var result = await adapter.RenameAsync(src, dest, overwrite: false);

        // Assert
        result.Key.ShouldBe(dest);
        result.Size.ShouldBe((ulong)stat.Size);
        result.ETag.ShouldBe("e");
    }
}

internal static class TestHelpers
{
    public static ObjectStat GetObjectStat(string objectKey, string etag = "", long size = 0, DateTime lastModified = default)
    {
        var stat = ObjectStat.FromResponseHeaders(objectKey, new Dictionary<string, string>()
        {
            { "etag", etag },
            {"content-length", size.ToString()},
            {"last-modified", lastModified == default ? DateTime.UtcNow.ToString() : lastModified.ToString()}
        });
        return stat;
    }

    // Helper to create IAsyncEnumerable<T> from array
    public static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var i in items)
        {
            yield return i;
            await Task.Yield();
        }
    }
}