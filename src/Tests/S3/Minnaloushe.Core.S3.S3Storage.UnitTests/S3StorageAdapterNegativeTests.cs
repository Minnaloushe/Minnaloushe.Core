using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Minnaloushe.Core.ClientProviders.Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Moq;
using Shouldly;

namespace Minnaloushe.Core.S3.S3Storage.UnitTests;

[TestFixture]
public class S3StorageAdapterNegativeTests
{
    private readonly Fixture _fixture = new();
    private readonly Mock<IMinioClient> _minioMock = new(MockBehavior.Strict);
    private readonly Mock<IMinioClientProvider> _clientProvider = new();
    private readonly ILogger<S3StorageAdapter> _nullLogger = NullLogger<S3StorageAdapter>.Instance;
    private S3StorageOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
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
        _clientProvider.SetupGet(x => x.Client)
            .Returns(_minioMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
    }

    [Test]
    public async Task GetStreamAsync_ThrowsS3StorageException_OnMinioException()
    {
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(m => m.GetObjectAsync(It.IsAny<GetObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new MinioException("minio fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        using var stream = new MemoryStream();

        await Should.ThrowAsync<S3StorageException>(() => adapter.GetStreamAsync(key, stream));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task GetStreamAsync_ThrowsS3StorageException_OnGenericException()
    {
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(m => m.GetObjectAsync(It.IsAny<GetObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        using var stream = new MemoryStream();

        await Should.ThrowAsync<S3StorageException>(() => adapter.GetStreamAsync(key, stream));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task GetTagsAsync_ThrowsS3StorageException_OnGenericException()
    {
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(m => m.GetObjectTagsAsync(It.IsAny<GetObjectTagsArgs>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("tags boom"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.GetTagsAsync(key));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task ListBlobsAsync_PropagatesMinioException_WhenMinioThrows()
    {
        _minioMock
            .Setup(m => m.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), CancellationToken.None))
            .Throws(new MinioException("list fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        // ListBlobsAsync does not wrap exceptions, so MinioException should propagate
        await Should.ThrowAsync<MinioException>(async () =>
        {
            await foreach (var _ in adapter.ListBlobsAsync())
            {
                // intentionally empty
            }
        });

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task PutAsync_ThrowsS3StorageException_OnMinioException()
    {
        var key = _fixture.Create<string>();
        using var ms = new System.IO.MemoryStream([1, 2, 3]);

        _minioMock
            .Setup(m => m.PutObjectAsync(It.IsAny<PutObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new MinioException("put fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.PutAsync(key, ms));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task PutAsync_ThrowsS3StorageException_OnGenericException()
    {
        var key = _fixture.Create<string>();
        using var ms = new System.IO.MemoryStream([1, 2, 3]);

        _minioMock
            .Setup(m => m.PutObjectAsync(It.IsAny<PutObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new Exception("generic put fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.PutAsync(key, ms));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task DeleteAsync_ThrowsS3StorageException_OnMinioException()
    {
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(m => m.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new MinioException("delete fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.DeleteAsync(key));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task DeleteAsync_ThrowsS3StorageException_OnGenericException()
    {
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(m => m.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new Exception("delete generic"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.DeleteAsync(key));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task ListAsync_ThrowsS3StorageException_OnMinioException()
    {
        _minioMock
            .Setup(m => m.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), CancellationToken.None))
            .Throws(new MinioException("list fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.ListAsync());

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task ListAsync_ThrowsS3StorageException_OnGenericException()
    {
        _minioMock
            .Setup(m => m.ListObjectsEnumAsync(It.IsAny<ListObjectsArgs>(), CancellationToken.None))
            .Throws(new Exception("list generic"));
        //.Returns(ThrowingAsync<ListItem>());

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.ListAsync());

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task GetBlobInfoAsync_ThrowsS3StorageException_OnMinioException()
    {
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(m => m.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new MinioException("stat fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.GetBlobInfoAsync(key));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task GetBlobInfoAsync_ThrowsS3StorageException_OnException()
    {
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(m => m.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new Exception("stat fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.GetBlobInfoAsync(key));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task ExistsAsync_ThrowsS3StorageException_OnException()
    {
        var key = _fixture.Create<string>();
        _minioMock
            .Setup(m => m.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new Exception("stat fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.ExistsAsync(key));

        _minioMock.VerifyAll();
    }


    [Test]
    public async Task RenameAsync_ThrowsS3StorageException_WhenCopyFails()
    {
        var src = "src.txt";
        var dest = "dest.txt";

        var stat = TestHelpers.GetObjectStat(dest, "\"e\"", 1);

        var seq = new MockSequence();

        // source exists
        _minioMock.InSequence(seq)
            .Setup(m => m.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ReturnsAsync(stat);

        // dest doesn't exist
        _minioMock.InSequence(seq)
            .Setup(m => m.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new ObjectNotFoundException("not found"));

        // Copy fails
        _minioMock.InSequence(seq)
            .Setup(m => m.CopyObjectAsync(It.IsAny<CopyObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new MinioException("copy fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.RenameAsync(src, dest));

        _minioMock.VerifyAll();
    }

    [Test]
    public async Task RenameAsync_ThrowsS3StorageException_WhenDeleteAfterCopyFails()
    {
        var src = "src.txt";
        var dest = "dest.txt";

        var stat = TestHelpers.GetObjectStat(dest, "\"e\"", 1);

        var seq = new MockSequence();

        // source exists
        _minioMock.InSequence(seq)
            .Setup(m => m.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ReturnsAsync(stat);

        // dest doesn't exist
        _minioMock.InSequence(seq)
            .Setup(m => m.StatObjectAsync(It.IsAny<StatObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new ObjectNotFoundException("not found"));

        // Copy succeeds
        _minioMock.InSequence(seq)
            .Setup(m => m.CopyObjectAsync(It.IsAny<CopyObjectArgs>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Delete (RemoveObjectAsync) fails
        _minioMock.InSequence(seq)
            .Setup(m => m.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), CancellationToken.None))
            .ThrowsAsync(new MinioException("remove fail"));

        var adapter = new S3StorageAdapter(_options, _clientProvider.Object, _nullLogger);

        await Should.ThrowAsync<S3StorageException>(() => adapter.RenameAsync(src, dest));

        _minioMock.VerifyAll();
    }

    // Helper: produce an IAsyncEnumerable<T> that throws when enumerated
    private static async IAsyncEnumerable<T> ThrowingAsync<T>(Exception ex)
    {
        await Task.Yield();
        throw ex;
#pragma warning disable 162
        yield break;
#pragma warning restore 162
    }
}