using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minnaloushe.Core.ClientProviders.Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Minnaloushe.Core.S3.S3Storage.LifecycleManagement;
using Moq;
using Shouldly;
using InternalLifecycleRule = Minnaloushe.Core.ClientProviders.Minio.Options.LifecycleRule;
using LifecycleRule = Minio.DataModel.ILM.LifecycleRule;


namespace Minnaloushe.Core.S3.S3Storage.IntegrationTests;

[Category("Integration")]
[Category("TestContainers")]
[TestFixture]
public class LifecycleRuleProcessorTests : IAsyncDisposable
{
    private const string TestBucketName = "lifecycle-test-bucket";
    private const string MinioUser = GlobalFixture.MinioUser;
    private const string MinioPassword = GlobalFixture.MinioPassword;

    private IMinioClient _minioClient = null!;
    private LifecycleRuleProcessor _synchronizer = null!;
    private string _endpoint = null!;
    private ILogger<LifecycleRuleProcessor> _logger = null!;
    private readonly Mock<IMinioClientProvider> _clientProviderMock = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Use global MinIO instance started by GlobalFixture
        _endpoint = GlobalFixture.Endpoint;

        // Create Minio client
        _minioClient = new MinioClient()
            .WithEndpoint(_endpoint.Replace("http://", ""))
            .WithCredentials(MinioUser, MinioPassword)
            .WithSSL(false)
            .Build();

        // Create logger
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LifecycleRuleProcessor>();

        // Create the test bucket
        await EnsureBucketExists();
    }

    [SetUp]
    public void SetUp()
    {
        _clientProviderMock.SetupGet(p => p.Client)
            .Returns(_minioClient);
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
        // Clean up any existing lifecycle rules before each test
        await CleanupLifecycleRules();
    }

    [TearDown]
    public async Task TestTearDown()
    {
        // Clean up lifecycle rules after each test
        await CleanupLifecycleRules();
    }

    private async Task EnsureBucketExists()
    {
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(TestBucketName);
        var bucketExists = await _minioClient.BucketExistsAsync(bucketExistsArgs);

        if (!bucketExists)
        {
            var makeBucketArgs = new MakeBucketArgs().WithBucket(TestBucketName);
            await _minioClient.MakeBucketAsync(makeBucketArgs);
        }
    }

    private async Task CleanupLifecycleRules()
    {
        try
        {
            var deleteBucketLifecycleArgs = new RemoveBucketLifecycleArgs().WithBucket(TestBucketName);
            await _minioClient.RemoveBucketLifecycleAsync(deleteBucketLifecycleArgs);
        }
        catch (Exception)
        {
            // Ignore errors - might not have lifecycle rules to delete
        }
    }

    private LifecycleRuleProcessor CreateSynchronizer(S3StorageOptions options)
    {
        return new LifecycleRuleProcessor(_clientProviderMock.Object, options, _logger);
    }

    private S3StorageOptions CreateOptions(InternalLifecycleRule[] lifecycleRules, bool syncRules = true, string? bucketName = null)
    {
        return new S3StorageOptions
        {
            ServiceUrl = _endpoint,
            AccessKey = MinioUser,
            SecretKey = MinioPassword,
            BucketName = bucketName ?? TestBucketName,
            SyncRules = syncRules,
            LifecycleRules = lifecycleRules
        };
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WhenSyncRulesIsDisabled_ShouldNotSyncRules()
    {
        // Arrange
        var options = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 7 }
        ], syncRules: false);

        _synchronizer = CreateSynchronizer(options);

        // Act
        await _synchronizer.SyncLifecycleRulesAsync();

        // Assert - No rules should be created
        var currentRules = await GetCurrentLifecycleRules();
        currentRules.ShouldBeEmpty();
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WithNewRules_ShouldCreateLifecycleRules()
    {
        // Arrange
        var options = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 7 },
            new InternalLifecycleRule { Prefix = "logs/", ExpirationInDays = 30 }
        ]);

        _synchronizer = CreateSynchronizer(options);

        // Act
        await _synchronizer.SyncLifecycleRulesAsync();

        // Assert
        var currentRules = await GetCurrentLifecycleRules();
        currentRules.Count.ShouldBe(2);

        var tempRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "temp/");
        tempRule.ShouldNotBeNull();
        tempRule.Expiration?.Days.ShouldBe(7);
        tempRule.Status.ShouldBe("Enabled");

        var logsRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "logs/");
        logsRule.ShouldNotBeNull();
        logsRule.Expiration?.Days.ShouldBe(30);
        logsRule.Status.ShouldBe("Enabled");
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WithSameRules_ShouldNotUpdateRules()
    {
        // Arrange - First, create initial rules
        var options = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 7 }
        ]);

        _synchronizer = CreateSynchronizer(options);
        await _synchronizer.SyncLifecycleRulesAsync();

        // Get the initial rules
        var initialRules = await GetCurrentLifecycleRules();
        var initialRuleId = initialRules[0].ID;

        // Act - Synchronize again with the same rules
        await _synchronizer.SyncLifecycleRulesAsync();

        // Assert - Rules should remain the same
        var currentRules = await GetCurrentLifecycleRules();
        currentRules.Count.ShouldBe(1);
        currentRules[0].ID.ShouldBe(initialRuleId);
        currentRules[0].Filter?.Prefix.ShouldBe("temp/");
        currentRules[0].Expiration?.Days.ShouldBe(7);
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WithDifferentRules_ShouldUpdateRules()
    {
        // Arrange - First, create initial rules
        var initialOptions = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 7 }
        ]);

        _synchronizer = CreateSynchronizer(initialOptions);
        await _synchronizer.SyncLifecycleRulesAsync();

        // Verify initial rules
        var initialRules = await GetCurrentLifecycleRules();
        initialRules.Count.ShouldBe(1);

        // Act - Update with different rules
        var updatedOptions = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 14 }, // Changed expiration
            new InternalLifecycleRule { Prefix = "archives/", ExpirationInDays = 90 } // New rule
        ]);

        _synchronizer = CreateSynchronizer(updatedOptions);
        await _synchronizer.SyncLifecycleRulesAsync();

        // Assert
        var currentRules = await GetCurrentLifecycleRules();
        currentRules.Count.ShouldBe(2);

        var tempRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "temp/");
        tempRule.ShouldNotBeNull();
        tempRule.Expiration?.Days.ShouldBe(14); // Should be updated

        var archivesRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "archives/");
        archivesRule.ShouldNotBeNull();
        archivesRule.Expiration?.Days.ShouldBe(90);
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WithDifferentRules_ShouldReplaceRules()
    {
        // Arrange - First, create initial rules
        var initialOptions = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 7 }
        ]);

        _synchronizer = CreateSynchronizer(initialOptions);
        await _synchronizer.SyncLifecycleRulesAsync();

        // Verify initial rules
        var initialRules = await GetCurrentLifecycleRules();
        initialRules.Count.ShouldBe(1);

        // Act - Update with different rules
        var updatedOptions = CreateOptions([
            new InternalLifecycleRule { Prefix = "archives/", ExpirationInDays = 90 } // New rule
        ]);

        _synchronizer = CreateSynchronizer(updatedOptions);
        await _synchronizer.SyncLifecycleRulesAsync();

        // Assert
        var currentRules = await GetCurrentLifecycleRules();
        currentRules.Count.ShouldBe(1);

        var tempRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "temp/");
        tempRule.ShouldBeNull();

        var archivesRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "archives/");
        archivesRule.ShouldNotBeNull();
        archivesRule.Expiration?.Days.ShouldBe(90);
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WithEmptyRules_ShouldRemoveAllRules()
    {
        // Arrange - First, create initial rules
        var initialOptions = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 7 },
            new InternalLifecycleRule { Prefix = "logs/", ExpirationInDays = 30 }
        ]);

        _synchronizer = CreateSynchronizer(initialOptions);
        await _synchronizer.SyncLifecycleRulesAsync();

        // Verify initial rules exist
        var initialRules = await GetCurrentLifecycleRules();
        initialRules.Count.ShouldBe(2);

        // Act - Update with empty rules
        var emptyOptions = CreateOptions([]); // Empty rules

        _synchronizer = CreateSynchronizer(emptyOptions);
        await _synchronizer.SyncLifecycleRulesAsync();

        // Assert - All rules should be removed
        var currentRules = await GetCurrentLifecycleRules();
        currentRules.ShouldBeEmpty();
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WithNonExistentBucket_ShouldSkipSynchronization()
    {
        // Arrange
        var options = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 7 }
        ], bucketName: "non-existent-bucket");

        _synchronizer = CreateSynchronizer(options);

        // Act & Assert - Should not throw exception
        await Should.NotThrowAsync(() => _synchronizer.SyncLifecycleRulesAsync());
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WithComplexPrefixes_ShouldCreateCorrectRules()
    {
        // Arrange
        var options = CreateOptions([
            new InternalLifecycleRule { Prefix = "app/logs/", ExpirationInDays = 7 },
            new InternalLifecycleRule { Prefix = "backup/*", ExpirationInDays = 365 },
            new InternalLifecycleRule { Prefix = "", ExpirationInDays = 30 } // Root prefix
        ]);

        _synchronizer = CreateSynchronizer(options);

        // Act
        await _synchronizer.SyncLifecycleRulesAsync();

        // Assert
        var currentRules = await GetCurrentLifecycleRules();
        currentRules.Count.ShouldBe(3);

        var appLogsRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "app/logs/");
        appLogsRule.ShouldNotBeNull();
        appLogsRule.Expiration?.Days.ShouldBe(7);

        var backupRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "backup/*");
        backupRule.ShouldNotBeNull();
        backupRule.Expiration?.Days.ShouldBe(365);

        var rootRule = currentRules.FirstOrDefault(r => r.Filter?.Prefix == "");
        rootRule.ShouldNotBeNull();
        rootRule.Expiration?.Days.ShouldBe(30);
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_WithInvalidCredentials_ShouldThrowS3WrapperRuleSynchronizationException()
    {
        // Arrange
        var invalidClient = new MinioClient()
            .WithEndpoint(_endpoint.Replace("http://", ""))
            .WithCredentials("invalid", "invalid")
            .WithSSL(false)
            .Build();
        _clientProviderMock.SetupGet(x => x.Client).Returns(invalidClient);

        var options = CreateOptions([
            new InternalLifecycleRule { Prefix = "temp/", ExpirationInDays = 7 }
        ]);

        var invalidSynchronizer = new LifecycleRuleProcessor(_clientProviderMock.Object, options, _logger);

        // Act & Assert
        await Should.ThrowAsync<S3StorageRuleSynchronizationException>(invalidSynchronizer.SyncLifecycleRulesAsync);

        invalidClient.Dispose();
    }

    [Test]
    public async Task SynchronizeLifecycleRulesAsync_RulePersistenceBetweenSynchronizations_ShouldMaintainConsistency()
    {
        // Arrange - Set up rules
        var options = CreateOptions([
            new InternalLifecycleRule { Prefix = "data/", ExpirationInDays = 60 }
        ]);

        _synchronizer = CreateSynchronizer(options);

        // Act - Multiple synchronizations
        await _synchronizer.SyncLifecycleRulesAsync();
        var firstSyncRules = await GetCurrentLifecycleRules();

        await _synchronizer.SyncLifecycleRulesAsync();
        var secondSyncRules = await GetCurrentLifecycleRules();

        await _synchronizer.SyncLifecycleRulesAsync();
        var thirdSyncRules = await GetCurrentLifecycleRules();

        // Assert - Rules should remain consistent
        firstSyncRules.Count.ShouldBe(1);
        secondSyncRules.Count.ShouldBe(1);
        thirdSyncRules.Count.ShouldBe(1);

        // All rule details should be identical
        var rule1 = firstSyncRules[0];
        var rule2 = secondSyncRules[0];
        var rule3 = thirdSyncRules[0];

        rule1.Filter?.Prefix.ShouldBe(rule2.Filter?.Prefix);
        rule1.Filter?.Prefix.ShouldBe(rule3.Filter?.Prefix);
        rule1.Expiration?.Days.ShouldBe(rule2.Expiration?.Days);
        rule1.Expiration?.Days.ShouldBe(rule3.Expiration?.Days);
    }

    private async Task<List<LifecycleRule>> GetCurrentLifecycleRules()
    {
        try
        {
            var getBucketLifecycleArgs = new GetBucketLifecycleArgs().WithBucket(TestBucketName);
            var lifecycleConfiguration = await _minioClient.GetBucketLifecycleAsync(getBucketLifecycleArgs);
            return lifecycleConfiguration.Rules?.ToList() ?? [];
        }
        catch (Exception)
        {
            // No lifecycle configuration exists
            return [];
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _minioClient.Dispose();

        GC.SuppressFinalize(this);
    }
}