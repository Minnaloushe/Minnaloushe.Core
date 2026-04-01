using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minnaloushe.Core.ClientProviders.Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.CompressedStorageAdapter;
using Minnaloushe.Core.S3.S3Storage.Models;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;
using Moq;
using Shouldly;
using System.IO.Compression;
using System.Text;

namespace Minnaloushe.Core.S3.S3Storage.IntegrationTests;

[Category("Integration")]
[Category("TestContainers")]
[TestFixture]
public class S3CompressedStorageAdapterTests : IAsyncDisposable
{
    private const string TestBucketName = "test-bucket";
    private const string MinioUser = GlobalFixture.MinioUser;
    private const string MinioPassword = GlobalFixture.MinioPassword;

    private S3StorageAdapter _adapter = null!;
    private IS3CompressedStorageAdapter _compressedAdapter = null!;
    private IMinioClient _minioClient = null!;
    private readonly Mock<IMinioClientProvider> _clientProvider = new();
    private readonly Mock<ITempStreamFactory> _tempStreamFactory = new();
    private string _endpoint = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Use global MinIO instance started by GlobalFixture
        _endpoint = GlobalFixture.Endpoint;

        _minioClient = new MinioClient()
            .WithEndpoint(_endpoint.Replace("http://", ""))
            .WithCredentials(MinioUser, MinioPassword)
            .WithSSL(false)
            .Build();

        _clientProvider.SetupGet(x => x.Client)
            .Returns(_minioClient);

        _tempStreamFactory.Setup(x => x.Create())
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

        var bucketExistsArgs = new BucketExistsArgs().WithBucket(TestBucketName);
        var bucketExists = await _minioClient.BucketExistsAsync(bucketExistsArgs);

        if (!bucketExists)
        {
            var makeBucketArgs = new MakeBucketArgs().WithBucket(TestBucketName);
            await _minioClient.MakeBucketAsync(makeBucketArgs);
        }

        _adapter = new S3StorageAdapter(
            options,
            _clientProvider.Object,
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<S3StorageAdapter>()
        );

        _compressedAdapter = new S3CompressedStorageAdapter(
            _adapter,
            _tempStreamFactory.Object,
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<S3CompressedStorageAdapter>()
        );
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _minioClient.Dispose();
        // Do not dispose global MinIO container here — GlobalFixture will handle it
    }

    [SetUp]
    public async Task TestSetUp()
    {
        await CleanupBucket();
    }

    [TearDown]
    public async Task TestTearDown()
    {
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
                    Console.WriteLine($"Error removing object {error.Key}: {error.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during bucket cleanup: {ex.Message}");
        }
    }

    private static byte[] CreateZipBytes(Dictionary<string, byte[]> entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kv in entries)
            {
                var entry = archive.CreateEntry(kv.Key);
                using var es = entry.Open();
                es.Write(kv.Value, 0, kv.Value.Length);
            }
        }

        ms.Position = 0;
        return ms.ToArray();
    }

    [Test]
    public async Task GetUncompressedAsync_WhenNotCompressed_ShouldReturnOriginalStream()
    {
        var key = "not-compressed.txt";
        var content = "plain content";
        var contentBytes = Encoding.UTF8.GetBytes(content);

        await using (var s = new MemoryStream(contentBytes))
        {
            await _adapter.PutAsync(key, s);
        }

        using var uncompressed = new MemoryStream();
        // Ensure no compressed tag is present
        await _compressedAdapter.GetUncompressedAsync(key, uncompressed);

        using var reader = new StreamReader(uncompressed, Encoding.UTF8);
        var read = await reader.ReadToEndAsync();
        read.ShouldBe(content);
    }

    [Test]
    public async Task GetUncompressedAsync_WhenCompressedButMissingContents_ShouldReturnOriginalZipStream()
    {
        var key = $"missingcontents/{Guid.NewGuid():N}";

        // Create a zip that only contains metadata
        var entries = new Dictionary<string, byte[]>
        {
            { "metadata", Encoding.UTF8.GetBytes("{}") }
        };

        var zipBytes = CreateZipBytes(entries);

        // Upload zip with compressed tag set
        await using (var ms = new MemoryStream(zipBytes))
        {
            await _adapter.PutAsync(key, ms, [new BlobTag("x-compressed", "true")], "application/zip");
        }

        await using var resultStream = new MemoryStream();
        // When getting uncompressed, since 'contents' entry is missing, should return original zip bytes
        await _compressedAdapter.GetUncompressedAsync(key, resultStream);

        var returned = resultStream.ToArray();

        returned.ShouldBe(zipBytes);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _minioClient.Dispose();

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
