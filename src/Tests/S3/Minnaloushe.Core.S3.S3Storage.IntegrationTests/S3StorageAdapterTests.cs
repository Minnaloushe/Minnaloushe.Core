using AutoFixture;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minnaloushe.Core.ClientProviders.Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Minnaloushe.Core.S3.S3Storage.Models;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;
using Moq;
using Shouldly;
using System.Security.Cryptography;
using System.Text;

namespace Minnaloushe.Core.S3.S3Storage.IntegrationTests;

[Category("Integration")]
[Category("TestContainers")]
[TestFixture]
public class S3StorageAdapterTests : IAsyncDisposable
{
    private const string TestBucketName = "test-bucket";
    private const string MinioUser = GlobalFixture.MinioUser;
    private const string MinioPassword = GlobalFixture.MinioPassword;

    private S3StorageAdapter _adapter = null!;
    private IMinioClient _minioClient = null!;
    private string _endpoint = null!;
    private readonly Mock<IMinioClientProvider> _clientProviderMock = new();
    private readonly Mock<ITempStreamFactory> _tempStreamFactoryMock = new();

    [OneTimeSetUp]
    public async Task SetUp()
    {
        // Use global MinIO container started by GlobalFixture
        _endpoint = GlobalFixture.Endpoint;

        _minioClient = new MinioClient()
            .WithEndpoint(_endpoint.Replace("http://", ""))
            .WithCredentials(MinioUser, MinioPassword)
            .WithSSL(false)
            .Build();

        _clientProviderMock.SetupGet(x => x.Client)
            .Returns(_minioClient);
        _tempStreamFactoryMock.Setup(x => x.Create())
            .Returns(() => new MemoryStream());

        var options = new S3StorageOptions
        {
            ServiceUrl = _endpoint,
            AccessKey = MinioUser,
            SecretKey = MinioPassword,
            BucketName = TestBucketName,
            SyncRules = false,
            LifecycleRules = []
        };

        // Create the test bucket if it doesn't exist
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(TestBucketName);
        var bucketExists = await _minioClient.BucketExistsAsync(bucketExistsArgs);

        if (!bucketExists)
        {
            var makeBucketArgs = new MakeBucketArgs().WithBucket(TestBucketName);
            await _minioClient.MakeBucketAsync(makeBucketArgs);
        }

        // Create adapter with Minio client
        _adapter = new S3StorageAdapter(
            options,
            _clientProviderMock.Object,
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<S3StorageAdapter>()
        );
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        _minioClient.Dispose();
        // Do not dispose global MinIO container here — GlobalFixture will handle it
    }

    [SetUp]
    public async Task TestSetUp()
    {
        // Clean up any existing objects in the test bucket before each test
        await CleanupBucket();
    }

    [TearDown]
    public async Task TestTearDown()
    {
        // Clean up any objects created during the test
        await CleanupBucket();
    }

    private async Task CleanupBucket()
    {
        try
        {
            var listObjectsArgs = new ListObjectsArgs()
                .WithBucket(TestBucketName)
                .WithRecursive(true);

            var objectsToDelete = new List<string>();

            await foreach (var item in _minioClient.ListObjectsEnumAsync(listObjectsArgs))
            {
                objectsToDelete.Add(item.Key);
            }

            if (objectsToDelete.Count > 0)
            {
                var removeObjectsArgs = new RemoveObjectsArgs()
                    .WithBucket(TestBucketName)
                    .WithObjects(objectsToDelete);

                foreach (var error in await _minioClient.RemoveObjectsAsync(removeObjectsArgs))
                {
                    // Log any errors but don't fail the test
                    Console.WriteLine($"Error removing object {error.Key}: {error.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log cleanup errors but don't fail the test
            Console.WriteLine($"Error during bucket cleanup: {ex.Message}");
        }
    }

    [Test]
    public async Task PutAsync_ShouldStoreFile()
    {
        // Arrange
        var key = "test-file.txt";
        var content = "Hello, World!";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var tags = new[] { new BlobTag("tag1", "value1"), new BlobTag("tag2", "value2") };

        // Act
        await using (var stream = new MemoryStream(contentBytes))
        {
            await _adapter.PutAsync(key, stream, tags, "text/plain");
        }

        // Assert
        // 1. Check if file exists and content matches
        using var outStream = new MemoryStream();

        await _adapter.GetStreamAsync(key, outStream);

        using (var reader = new StreamReader(outStream))
        {
            var retrievedContent = await reader.ReadToEndAsync();
            retrievedContent.ShouldBe(content);
        }

        // 2. Check if tags were stored
        var retrievedTags = await _adapter.GetTagsAsync(key);
        retrievedTags.Count.ShouldBe(2);
        retrievedTags.ShouldContain(t => t.Key == "tag1" && t.Value == "value1");
        retrievedTags.ShouldContain(t => t.Key == "tag2" && t.Value == "value2");
    }

    [Test]
    public async Task DeleteAsync_ShouldRemoveFile()
    {
        // Arrange
        var key = "file-to-delete.txt";
        var content = "Delete me!";
        var contentBytes = Encoding.UTF8.GetBytes(content);

        await using (var stream = new MemoryStream(contentBytes))
        {
            await _adapter.PutAsync(key, stream);
        }

        // Act
        await _adapter.DeleteAsync(key);

        using var outStream = new MemoryStream();
        // Assert
        await Should.ThrowAsync<S3StorageException>(() => _adapter.GetStreamAsync(key, outStream));
    }

    [Test]
    public async Task ListAsync_ShouldReturnFiles()
    {
        // Arrange
        var keys = new[] { "test1.txt", "test2.txt", "other/test3.txt" };
        var content = "Test content";
        var contentBytes = Encoding.UTF8.GetBytes(content);

        foreach (var key in keys)
        {
            await using var stream = new MemoryStream(contentBytes);
            await _adapter.PutAsync(key, stream);
        }

        // Act
        var result = await _adapter.ListAsync();

        // Assert
        result.Count.ShouldBe(keys.Length);
        foreach (var key in keys)
        {
            result.ShouldContain(x => x.Key == key);
        }
    }

    [Test]
    public async Task ListAsync_WithPrefix_ShouldReturnFilteredFiles()
    {
        // Arrange
        var prefix = "filtered/";
        var matchingKeys = new[] { "filtered/test1.txt", "filtered/test2.txt" };
        var nonMatchingKeys = new[] { "other/test3.txt", "test4.txt" };
        var content = "Test content";
        var contentBytes = Encoding.UTF8.GetBytes(content);

        foreach (var key in matchingKeys.Concat(nonMatchingKeys))
        {
            await using var stream = new MemoryStream(contentBytes);
            await _adapter.PutAsync(key, stream);
        }

        // Act
        var result = await _adapter.ListAsync(prefix);

        // Assert
        result.Count.ShouldBe(matchingKeys.Length);
        foreach (var key in matchingKeys)
        {
            result.ShouldContain(x => x.Key == key);
        }
        foreach (var key in nonMatchingKeys)
        {
            result.ShouldNotContain(x => x.Key == key);
        }
    }

    [Test]
    public async Task GetStreamAsync_NonExistentFile_ShouldThrowException()
    {
        // Arrange
        var key = "non-existent-file.txt";

        using var outStream = new MemoryStream();
        // Act & Assert
        await Should.ThrowAsync<S3StorageException>(() => _adapter.GetStreamAsync(key, outStream));
    }

    [Test]
    public async Task GetTagsAsync_NonExistentFile_ShouldThrowException()
    {
        // Arrange
        var key = "non-existent-file.txt";

        // Act & Assert
        await Should.ThrowAsync<S3StorageException>(() => _adapter.GetTagsAsync(key));
    }

    [Test]
    public async Task PutAsync_WithoutTags_ShouldStoreFile()
    {
        // Arrange
        var key = "test-file-no-tags.txt";
        var content = "Hello, World without tags!";
        var contentBytes = Encoding.UTF8.GetBytes(content);

        // Act
        await using (var stream = new MemoryStream(contentBytes))
        {
            await _adapter.PutAsync(key, stream);
        }

        // Assert
        using var outStream = new MemoryStream();

        await _adapter.GetStreamAsync(key, outStream);

        using (var reader = new StreamReader(outStream))
        {
            var retrievedContent = await reader.ReadToEndAsync();
            retrievedContent.ShouldBe(content);
        }

        var retrievedTags = await _adapter.GetTagsAsync(key);
        retrievedTags.Count.ShouldBe(0);
    }

    // NEW: Tests for missing methods

    [Test]
    public async Task ExistsAsync_ShouldReflectPresence()
    {
        var key = "exists-test.txt";
        var contentBytes = Encoding.UTF8.GetBytes("exists content");

        (await _adapter.ExistsAsync(key)).ShouldBeFalse();

        await using (var stream = new MemoryStream(contentBytes))
        {
            await _adapter.PutAsync(key, stream);
        }

        (await _adapter.ExistsAsync(key)).ShouldBeTrue();

        await _adapter.DeleteAsync(key);

        (await _adapter.ExistsAsync(key)).ShouldBeFalse();
    }

    [Test]
    public async Task GetBlobInfoAsync_ShouldReturnMetadata()
    {
        var key = "meta-test.txt";
        var content = "metadata content";
        var contentBytes = Encoding.UTF8.GetBytes(content);

        //using var stream = new MemoryStream(contentBytes);
        //var returned = await _adapter.PutAsync(key, stream); // compiler-friendly; actual call below

        // call PutAsync and capture BlobInfo properly
        await using (var putStream = new MemoryStream(contentBytes))
        {
            var putInfo = await _adapter.PutAsync(key, putStream);
            putInfo.Key.ShouldBe(key);
            putInfo.Size.ShouldBe((ulong)contentBytes.Length);
            putInfo.ETag.ShouldNotBeNullOrEmpty();
            // LastModified may be null depending on implementation timing; ensure present or at least not throw
            // If adapter returns null, we don't fail the test here; check it's either null or recent.
            if (putInfo.LastModified != null)
            {
                (DateTimeOffset.UtcNow - putInfo.LastModified.Value).ShouldBeLessThan(TimeSpan.FromMinutes(5));
            }
        }

        var info = await _adapter.GetBlobInfoAsync(key);
        info.Key.ShouldBe(key);
        info.Size.ShouldBe((ulong)contentBytes.Length);
        info.ETag.ShouldNotBeNullOrEmpty();
        info.LastModified.ShouldNotBeNull();
    }

    [Test]
    public async Task RenameAsync_ShouldMoveFile()
    {
        var existingKey = "to-rename.txt";
        var newKey = "renamed.txt";
        var content = "rename me";
        var bytes = Encoding.UTF8.GetBytes(content);

        await using (var s = new MemoryStream(bytes))
        {
            await _adapter.PutAsync(existingKey, s);
        }
        (await _adapter.ExistsAsync(existingKey)).ShouldBeTrue();
        (await _adapter.ExistsAsync(newKey)).ShouldBeFalse();

        var result = await _adapter.RenameAsync(existingKey, newKey);
        result.Key.ShouldBe(newKey);

        (await _adapter.ExistsAsync(newKey)).ShouldBeTrue();
        (await _adapter.ExistsAsync(existingKey)).ShouldBeFalse();

        using var outStream = new MemoryStream();
        // content preserved
        await _adapter.GetStreamAsync(newKey, outStream);
        using var reader = new StreamReader(outStream);
        (await reader.ReadToEndAsync()).ShouldBe(content);
    }

    [Test]
    public async Task RenameAsync_ShouldThrowWhenTargetExists_UnlessOverwriteTrue()
    {
        var existingKey = "rename-src.txt";
        var newKey = "rename-dest.txt";
        var srcContent = "src";
        var destContent = "dest";

        await using (var s = new MemoryStream(Encoding.UTF8.GetBytes(srcContent)))
        {
            await _adapter.PutAsync(existingKey, s);
        }

        await using (var s2 = new MemoryStream(Encoding.UTF8.GetBytes(destContent)))
        {
            await _adapter.PutAsync(newKey, s2);
        }

        // rename without overwrite should throw
        await Should.ThrowAsync<S3StorageException>(() => _adapter.RenameAsync(existingKey, newKey));

        // rename with overwrite should succeed and replace dest content
        var info = await _adapter.RenameAsync(existingKey, newKey, overwrite: true);
        info.Key.ShouldBe(newKey);

        using var outStream = new MemoryStream();

        await _adapter.GetStreamAsync(newKey, outStream);
        using var reader = new StreamReader(outStream);
        (await reader.ReadToEndAsync()).ShouldBe(srcContent);
    }

    [Test]
    public async Task PutAsync_ShouldReturnBlobInfo()
    {
        var key = "put-return-info.txt";
        var content = "info content";
        var bytes = Encoding.UTF8.GetBytes(content);

        await using var s = new MemoryStream(bytes);

        var info = await _adapter.PutAsync(key, s);
        info.ShouldNotBeNull();
        info.Key.ShouldBe(key);
        info.Size.ShouldBe((ulong)bytes.Length);
        info.ETag.ShouldNotBeNullOrEmpty();
        info.LastModified.ShouldNotBeNull();
    }

    [Test]
    public async Task ListBlobsAsync_ShouldStreamAllBlobs()
    {
        var keys = new[] { "stream1.txt", "stream2.txt", "folder/stream3.txt" };
        var bytes = Encoding.UTF8.GetBytes("stream content");

        foreach (var k in keys)
        {
            await using var s = new MemoryStream(bytes);
            await _adapter.PutAsync(k, s);
        }

        var collected = new List<BlobInfo>();
        await foreach (var b in _adapter.ListBlobsAsync())
        {
            collected.Add(b);
        }

        collected.Count.ShouldBe(keys.Length);
        foreach (var k in keys)
        {
            collected.ShouldContain(x => x.Key == k);
        }
    }

    internal record TestRecord(string Key, string Data, byte[] Hash, string FileName, int HashType, string Source);

    [Test]
    public async Task PutAsync_ShouldStoreInpxMetadata()
    {
        var fixture = new Fixture();
        fixture.Customize<DateOnly>(o => o.FromFactory((DateTime dt) => DateOnly.FromDateTime(dt)));

        var data = fixture.Create<byte[]>();

        var hash = SHA256.HashData(data);

        var file = fixture.Build<TestRecord>()
            .With(f => f.Key, $"books/{fixture.Create<string>()}")
            .With(f => f.Hash, hash)
            .With(f => f.FileName, "/file234.fb2")
            .With(f => f.HashType, 1)
            .Create();


        await using var ms = new MemoryStream();
        await ms.WriteAsync(data, 0, data.Length);
        ms.Position = 0;

        //var result = await _minioClient.PutObjectAsync(
        //    new PutObjectArgs()
        //        .WithBucket("test-bucket")
        //        .WithObject(file.Key)
        //        .WithStreamData(ms)
        //        .WithTagging(Tagging.GetObjectTags(new Dictionary<string, string>()
        //            {
        //                {
        //                    "test-tag", "value"
        //                }
        //            })
        //        )
        //);

        BlobTag[] tags =
        [
            new("inpx-file-id", file.Key),
            //new BlobTag("inpx-file-source", JsonConvert.SerializeObject(file.Source)),
            new("inpx-file-hash", Convert.ToHexString(file.Hash)),
            new("inpx-file-source", Convert.ToBase64String(Encoding.UTF8.GetBytes(file.Source))),
            new("inpx-file-name", file.FileName),
            new("inpx-file-hash-type", file.HashType.ToString())
        ];
        var result = await _adapter.PutAsync(file.Key, ms, tags, "application/octet-stream");

        var info = await _adapter.GetTagsAsync(file.Key);

        info.Count.ShouldBe(5);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _minioClient.Dispose();

        // Do not dispose global MinIO container here — GlobalFixture will handle it
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}