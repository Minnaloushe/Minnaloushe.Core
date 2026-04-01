using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.Migrations.MongoDb;
using Minnaloushe.Core.Repositories.MongoDb.Extensions;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Minnaloushe.Core.Repositories.MongoDb.Tests;

[Category("Integration")]
[Category("TestContainers")]
public class MongoDbRepositoryIntegrationTests
{
    #region Constants

    private const string ConnectionName = "mongo-connection";
    private const string RepositoryName = "mongo-repository";

    #endregion

    #region Fields

    private TestHost _sut = null!;

    #endregion

    #region Setups and Teardowns

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _sut = await TestHost.Build(
            configureConfiguration: cfg => cfg.AddConfiguration(AppSettings),
            configureServices: (services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                services.AddSingleton(configuration);
                services.ConfigureAsyncInitializers();

                services.AddSingleton<ITestRepository, TestRepository>();

                services.AddMongoDbMigration<InitialMigration>();
                services.AddMongoDbMigration<SoftDeleteMigration>();

                services.AddRepositories(configuration)
                    .AddMongoDbClientProviders(() =>
                    {
                        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    })
                    .AddMongoDbMigrations()
                    .Build();
            },
            beforeStart: async host =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: true);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    private static object AppSettings =>
        new
        {
            RepositoryConfiguration = new
            {
                Connections = new[]
                {
                    new
                    {
                        Name = ConnectionName,
                        Type = "mongodb",
                        ConnectionString =
                            $"mongodb://{GlobalFixture.MongoDb.Username}:{GlobalFixture.MongoDb.Password}" +
                            $"@{GlobalFixture.MongoDb.Host}:{GlobalFixture.MongoDb.Port}" +
                            $"/?authSource={GlobalFixture.MongoDb.AuthDb}",
                        DatabaseName = GlobalFixture.MongoDb.AppDb
                    }
                },
                Repositories = new[]
                {
                    new
                    {
                        Name = RepositoryName,
                        ConnectionName,
                        Migrations = new { Enabled = true }
                    }
                }
            },
            AsyncInitializer = new
            {
                Enabled = true,
                Timeout = TimeSpan.FromMinutes(1)
            }
        };

    private IMongoDatabase GetDatabase()
    {
        var clientProvider = _sut.Services.GetRequiredKeyedService<IMongoClientProvider>(ConnectionName);
        var options = _sut.Services.GetRequiredService<IOptionsMonitor<RepositoryOptions>>().Get(RepositoryName);
        using var lease = clientProvider.Acquire();
        return lease.Client.GetDatabase(options.DatabaseName);
    }

    #endregion

    [Test]
    public void WhenResolvingRepositoryOptions_ThenShouldNotBeNull()
    {
        //Arrange

        using var scope = _sut.Services.CreateScope();

        //Act
        var options = scope.ServiceProvider.GetService<IOptionsMonitor<RepositoryOptions>>();
        var value = options?.Get(RepositoryName);

        //Assert

        using var assertionScope = new AssertionScope();

        options.Should().NotBeNull("Repository options should be registered for the repository");
        value.Should().NotBeNull();
        value.ConnectionName.Should().Be(ConnectionName, "Repository options should have the correct connection name");
        value.DatabaseName.Should().Be(GlobalFixture.MongoDb.AppDb, "Repository options should have the correct database name");
    }

    [Test]
    public void WhenResolvingProviderByConnectionName_ThenShouldNotBeNull()
    {
        // Arrange & Act
        var provider = _sut.Services.GetKeyedService<IMongoClientProvider>(ConnectionName);

        // Assert
        provider.Should().NotBeNull("MongoDB client provider should be registered for the connection");
    }

    [Test]
    public void WhenResolvingProviderByRepositoryAndConnectionName_ThenShouldReturnSameInstance()
    {
        // Arrange & Act
        var providerByConnection = _sut.Services.GetKeyedService<IMongoClientProvider>(ConnectionName);
        var providerByRepository = _sut.Services.GetKeyedService<IMongoClientProvider>(RepositoryName);

        // Assert
        using var scope = new AssertionScope();
        providerByConnection.Should().NotBeNull();
        providerByRepository.Should().Be(providerByConnection, "the same client provider should be resolved by both connection name and repository name");
    }

    [Test]
    public async Task InitialMigration_WhenExecuted_ThenUniqueIndexOnUniqueKeyExists()
    {
        // Arrange
        var collection = GetDatabase().GetCollection<TestEntity>(nameof(TestEntity));

        // Act
        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();

        // Assert
        indexes.Should().Contain(i =>
            i.Contains("name") && i["name"].AsString == "ix_TestEntity_UniqueKey" &&
            i.Contains("unique") && i["unique"].AsBoolean);
    }

    [Test]
    public async Task SoftDeleteMigration_WhenExecuted_ThenTtlIndexOnDeletedAtExists()
    {
        // Arrange
        var collection = GetDatabase().GetCollection<TestEntity>(nameof(TestEntity));

        // Act
        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();

        // Assert
        indexes.Should().Contain(i =>
            i.Contains("name") && i["name"].AsString == "ix_TestEntity_DeletedAt_ttl" &&
            i.Contains("expireAfterSeconds"));
    }

    [Test]
    public async Task Migrations_WhenApplied_ThenTrackedInMigrationsCollection()
    {
        // Arrange
        var migrationsCollection = GetDatabase().GetCollection<BsonDocument>("__migrations");

        // Act
        var appliedMigrations = await migrationsCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var appliedIds = appliedMigrations.Select(m => m["_id"].AsString).ToList();

        // Assert
        using var scope = new AssertionScope();
        appliedIds.Should().Contain("01_InitialMigration");
        appliedIds.Should().Contain("02_SoftDeleteMigration");
    }

    [Test]
    public async Task UpsertAsync_WhenEntityUpserted_ThenCanBeRetrievedByUniqueKey()
    {
        // Arrange
        var repository = _sut.Services.GetRequiredService<ITestRepository>();
        var entity = new TestEntity { Data = "test-data", UniqueKey = Guid.NewGuid() };

        // Act
        await repository.UpsertAsync(entity);
        var result = await repository.GetAsync(entity.UniqueKey);

        // Assert
        using var scope = new AssertionScope();
        result.Should().NotBeNull();
        result.Data.Should().Be(entity.Data);
        result.UniqueKey.Should().Be(entity.UniqueKey);
    }

    [Test]
    public async Task DeleteAsync_WhenEntityDeleted_ThenGetReturnsNull()
    {
        // Arrange
        var repository = _sut.Services.GetRequiredService<ITestRepository>();
        var entity = new TestEntity { Data = "to-delete", UniqueKey = Guid.NewGuid() };
        await repository.UpsertAsync(entity);

        // Act
        await repository.DeleteAsync(entity.UniqueKey);
        var result = await repository.GetAsync(entity.UniqueKey);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task UpsertAsync_WhenEntityUpsertedTwice_ThenOnlyOneDocumentExists()
    {
        // Arrange
        var repository = _sut.Services.GetRequiredService<ITestRepository>();
        var uniqueKey = Guid.NewGuid();
        var original = new TestEntity { Data = "original", UniqueKey = uniqueKey };
        var updated = new TestEntity { Data = "updated", UniqueKey = uniqueKey };

        // Act
        await repository.UpsertAsync(original);
        await repository.UpsertAsync(updated);
        var collection = GetDatabase().GetCollection<TestEntity>(nameof(TestEntity));
        var count = await collection.CountDocumentsAsync(Builders<TestEntity>.Filter.Eq(e => e.UniqueKey, uniqueKey));

        // Assert
        count.Should().Be(1);
    }

    #region Helper Classes

    internal record TestEntity : ISoftDeleteEntity
    {
        [BsonId]
        public ObjectId Id { get; init; }

        public string Data { get; init; } = string.Empty;
        public Guid UniqueKey { get; init; }
        public DateTimeOffset? DeletedAt { get; init; }
    }

    internal class TestRepository(
        [FromKeyedServices(RepositoryName)]
        IMongoClientProvider clientProvider,
        IOptionsMonitor<RepositoryOptions> optionsMonitor
        )
        : MongoDbRepositoryBase(optionsMonitor.Get(RepositoryName))
        , ITestRepository
    {
        public async Task<TestEntity?> GetAsync(Guid key)
        {
            using var lease = clientProvider.Acquire();

            return (await GetCollection<TestEntity>(lease.Client)
                .FindAsync(
                filter: Builders<TestEntity>.Filter.Eq(e => e.UniqueKey, key) & Builders<TestEntity>.Filter.Eq(e => e.DeletedAt, null)
                )).FirstOrDefault();
        }

        public async Task DeleteAsync(Guid key)
        {
            using var lease = clientProvider.Acquire();

            await GetCollection<TestEntity>(lease.Client)
                .UpdateOneAsync(
                    filter: Builders<TestEntity>.Filter.Eq(e => e.UniqueKey, key) & Builders<TestEntity>.Filter.Eq(e => e.DeletedAt, null),
                    update: Builders<TestEntity>.Update.Set(e => e.DeletedAt, DateTimeOffset.UtcNow)
                );
        }

        public async Task<TestEntity> UpsertAsync(TestEntity entity)
        {
            using var lease = clientProvider.Acquire();

            return await GetCollection<TestEntity>(lease.Client)
                .FindOneAndUpdateAsync(
                    filter: Builders<TestEntity>.Filter.Eq(e => e.UniqueKey, entity.UniqueKey),
                    update: Builders<TestEntity>.Update
                        .SetOnInsert(e => e.Id, ObjectId.GenerateNewId())
                        .Set(e => e.Data, entity.Data)
                        .Set(e => e.DeletedAt, null),
                    options: new FindOneAndUpdateOptions<TestEntity>
                    {
                        IsUpsert = true,
                        ReturnDocument = ReturnDocument.After
                    }
                );
        }
    }

    internal interface ITestRepository
    {
        Task<TestEntity?> GetAsync(Guid key);
        Task DeleteAsync(Guid key);
        Task<TestEntity> UpsertAsync(TestEntity entity);
    }

    internal class InitialMigration(
        [FromKeyedServices(RepositoryName)] IMongoClientProvider clientProvider,
        IOptionsMonitor<RepositoryOptions> optionsMonitor) : IMongoDbMigration
    {
        public string Id => "01_InitialMigration";
        public string TargetRepository => RepositoryName;

        public async Task UpAsync(CancellationToken cancellationToken)
        {
            var options = optionsMonitor.Get(RepositoryName);
            using var lease = clientProvider.Acquire();
            var collection = lease.Client
                .GetDatabase(options.DatabaseName)
                .GetCollection<TestEntity>(nameof(TestEntity));

            var indexModel = new CreateIndexModel<TestEntity>(
                Builders<TestEntity>.IndexKeys.Ascending(e => e.UniqueKey),
                new CreateIndexOptions { Unique = true, Name = "ix_TestEntity_UniqueKey" });

            await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
        }
    }

    internal class SoftDeleteMigration(
        [FromKeyedServices(RepositoryName)] IMongoClientProvider clientProvider,
        IOptionsMonitor<RepositoryOptions> optionsMonitor) : IMongoDbMigration
    {
        public string Id => "02_SoftDeleteMigration";
        public string TargetRepository => RepositoryName;

        public async Task UpAsync(CancellationToken cancellationToken)
        {
            var options = optionsMonitor.Get(RepositoryName);
            using var lease = clientProvider.Acquire();
            var collection = lease.Client
                .GetDatabase(options.DatabaseName)
                .GetCollection<TestEntity>(nameof(TestEntity));

            var indexModel = new CreateIndexModel<TestEntity>(
                Builders<TestEntity>.IndexKeys.Ascending(e => e.DeletedAt),
                new CreateIndexOptions { Name = "ix_TestEntity_DeletedAt_ttl", ExpireAfter = options.DeletedTtl });

            await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
        }
    }

    #endregion
}
