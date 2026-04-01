using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.MongoDb.Extensions;
using Minnaloushe.Core.Repositories.MongoDb.Vault.Extensions;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Entities;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Minnaloushe.Core.VaultService.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Tests.Integration;

[Category("Integration")]
[Category("TestContainers")]
public class MongoDbVaultIntegrationTests
{
    #region Constants

    private const string RepositoryName1 = "vault-repo-1";
    private const string RepositoryName2 = "vault-repo-2";
    private const string ConnectionName1 = "vault-mongo-connection-1";
    private const string ConnectionName2 = "vault-mongo-connection-2";
    private const string ServiceNameVault = "vault";

    #endregion

    private TestHost _sut = null!;
    private readonly Mock<IServiceDiscoveryService> _mockServiceDiscovery = new();
    private readonly Mock<IInfrastructureConventionProvider> _mockDependenciesProvider = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        #region Prepare mocks

        _mockServiceDiscovery
            .Setup(x => x.ResolveServiceEndpoint(
                GlobalFixture.MongoDbInstance1.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ServiceEndpoint
                {
                    Host = GlobalFixture.MongoDbInstance1.Host,
                    Port = GlobalFixture.MongoDbInstance1.Port
                }
            ]);
        _mockServiceDiscovery
            .Setup(x => x.ResolveServiceEndpoint(
                GlobalFixture.MongoDbInstance2.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ServiceEndpoint
                {
                    Host = GlobalFixture.MongoDbInstance2.Host,
                    Port = GlobalFixture.MongoDbInstance2.Port
                }
            ]);

        _mockDependenciesProvider
            .Setup(x => x.GetDatabaseRole(GlobalFixture.MongoDbInstance1.Name, It.IsAny<string>()))
            .ReturnsAsync(GlobalFixture.Vault.GetDbRoleName(GlobalFixture.MongoDbInstance1));
        _mockDependenciesProvider
            .Setup(x => x.GetDatabaseRole(GlobalFixture.MongoDbInstance2.Name, It.IsAny<string>()))
            .ReturnsAsync(GlobalFixture.Vault.GetDbRoleName(GlobalFixture.MongoDbInstance2));
        _mockDependenciesProvider
            .Setup(x => x.GetConsulServiceName(GlobalFixture.MongoDbInstance1.Name))
            .ReturnsAsync(GlobalFixture.MongoDbInstance1.Name);
        _mockDependenciesProvider
            .Setup(x => x.GetConsulServiceName(GlobalFixture.MongoDbInstance2.Name))
            .ReturnsAsync(GlobalFixture.MongoDbInstance2.Name);

        #endregion

        #region Build TestHost using AppConfig

        _sut = await TestHost.Build(
            configureConfiguration: cfg => cfg.AddConfiguration(AppSettings),
            configureServices: (services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                // Add configuration
                services.AddSingleton(configuration);

                // Add mocks
                services.AddSingleton(_mockServiceDiscovery.Object);
                services.AddSingleton(_mockDependenciesProvider.Object);

                services.ConfigureAsyncInitializers();

                // Register required infrastructure services
                services.AddVaultClientProvider();

                services.AddRepositories(configuration)
                    .AddMongoDbClientProviders()
                    .AddVaultMongoDbClientProviders()
                    .Build();
            },
            beforeStart: async host =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: false);
        #endregion
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }

    private static object AppSettings =>
        new
        {
            Vault = new
            {
                Address = GlobalFixture.Vault.VaultAddress,
                Token = GlobalFixture.Vault.Password,
                Scheme = "http",
                ServiceName = ServiceNameVault
            },
            SerivceDiscovery = new
            {
                ConsulService = GlobalFixture.Consul.Host,
                ConsulPort = GlobalFixture.Consul.Port
            },
            RepositoryConfiguration = new
            {
                Connections = new[]
                {
                    new
                    {
                        Name = ConnectionName1,
                        Type = "mongodb",
                        ServiceName = GlobalFixture.MongoDbInstance1.Name,
                        LeaseRenewInterval = "00:00:30",
                        DatabaseName = GlobalFixture.MongoDbInstance1.AppDb
                    },
                    new
                    {
                        Name = ConnectionName2,
                        Type = "mongodb",
                        ServiceName = GlobalFixture.MongoDbInstance2.Name,
                        LeaseRenewInterval = "00:00:30",
                        DatabaseName = GlobalFixture.MongoDbInstance2.Name
                    }
                },
                Repositories = new[]
                {
                    new
                    {
                        Name = RepositoryName1,
                        ConnectionName = ConnectionName1,
                    },
                    new
                    {
                        Name = RepositoryName2,
                        ConnectionName = ConnectionName2,
                    }
                }
            },
            AsyncInitializer = new
            {
                Enabled = true,
                Timeout = TimeSpan.FromMinutes(1)
            }
        };

    [Test]
    public void WhenResolvingFirstProviderByConnectionNameProvider1ShouldNotBeNull()
    {
        var providerByConnection1 = _sut.Services.GetKeyedService<IMongoClientProvider>(ConnectionName1);
        providerByConnection1.Should().NotBeNull("First MongoDB client provider should be registered");
    }

    [Test]
    public void WhenResolvingSecondProviderByConnectionNameProvider2ShouldNotBeNull()
    {
        var providerByConnection2 = _sut.Services.GetKeyedService<IMongoClientProvider>(ConnectionName2);
        providerByConnection2.Should().NotBeNull("Second MongoDB client provider should be registered");
    }

    [Test]
    public void WhenResolvingFirstProviderByConnectionAndRepositoryNameThenShouldReturnSameInstance()
    {
        var providerByConnection1 = _sut.Services.GetKeyedService<IMongoClientProvider>(ConnectionName1);
        var providerByRepository1 = _sut.Services.GetKeyedService<IMongoClientProvider>(RepositoryName1);
        providerByConnection1.Should().Be(providerByRepository1, "For first connection same client provider should be resolved by connection name and repository name");
    }

    [Test]
    public void WhenResolvingSecondProviderByConnectionAndRepositoryNameThenShouldReturnSameInstance()
    {
        var providerByConnection2 = _sut.Services.GetKeyedService<IMongoClientProvider>(ConnectionName2);
        var providerByRepository2 = _sut.Services.GetKeyedService<IMongoClientProvider>(RepositoryName2);
        providerByConnection2.Should().Be(providerByRepository2, "For second connection same client provider should be resolved by connection name and repository name");
    }

    [Test]
    public void WhenResolvingClientProvidersThenShouldNotReturnSameInstanceForFirstAndSecondConnection()
    {
        // Assert providers exist
        var providerByConnection1 = _sut.Services.GetKeyedService<IMongoClientProvider>(ConnectionName1);

        var providerByConnection2 = _sut.Services.GetKeyedService<IMongoClientProvider>(ConnectionName2);

        providerByConnection1.Should().NotBe(providerByConnection2, "Resolved providers should not reference same instance");
    }

    [Test]
    public void WhenResolvingClientProvidersThenProvidersShouldReferenceDifferentHosts()
    {
        var providerByConnection1 = _sut.Services.GetRequiredKeyedService<IMongoClientProvider>(ConnectionName1);
        var providerByConnection2 = _sut.Services.GetRequiredKeyedService<IMongoClientProvider>(ConnectionName2);

        using var lease1 = providerByConnection1.Acquire();
        using var lease2 = providerByConnection2.Acquire();

        lease1.Client.Settings.Server.Port.Should().NotBe(lease2.Client.Settings.Server.Port, "Providers should reference different hosts");
    }

    [Test]
    public async Task WhenAccessingDatabase1ThenShouldInsertAndRetrieveData()
    {
        var mongoProvider1 = _sut.Services.GetRequiredKeyedService<IMongoClientProvider>(ConnectionName1);
        using var clientLease1 = mongoProvider1.Acquire();
        var mongoClient1 = clientLease1.Client;

        var database1 = mongoClient1.GetDatabase(GlobalFixture.MongoDbInstance1.AppDb);
        var collection1 = database1.GetCollection<BsonDocument>("test-collection-1");

        var testDocument1 = new BsonDocument
        {
            { "name", "test-document-1" },
            { "connection", ConnectionName1 },
            { "timestamp", DateTime.UtcNow },
            { "value", 100 }
        };

        await collection1.InsertOneAsync(testDocument1);

        var filter1 = Builders<BsonDocument>.Filter.Eq("name", "test-document-1");
        var retrievedDocument1 = await collection1.Find(filter1).FirstOrDefaultAsync();

        retrievedDocument1.Should().NotBeNull("Document should be retrieved from first MongoDB");
        retrievedDocument1["name"].AsString.Should().Be("test-document-1");
        retrievedDocument1["value"].AsInt32.Should().Be(100);
    }

    [Test]
    public async Task WhenAccessingDatabase2ThenShouldInsertAndRetrieveData()
    {
        var mongoProvider2 = _sut.Services.GetRequiredKeyedService<IMongoClientProvider>(ConnectionName2);

        using var clientLease2 = mongoProvider2.Acquire();
        var mongoClient2 = clientLease2.Client;

        var database2 = mongoClient2.GetDatabase(GlobalFixture.MongoDbInstance2.AppDb);
        var collection2 = database2.GetCollection<BsonDocument>("test-collection-2");

        var testDocument2 = new BsonDocument
        {
            { "name", "test-document-2" },
            { "connection", ConnectionName2 },
            { "timestamp", DateTime.UtcNow },
            { "value", 200 }
        };

        await collection2.InsertOneAsync(testDocument2);

        var filter2 = Builders<BsonDocument>.Filter.Eq("name", "test-document-2");
        var retrievedDocument2 = await collection2.Find(filter2).FirstOrDefaultAsync();

        retrievedDocument2.Should().NotBeNull("Document should be retrieved from second MongoDB");
        retrievedDocument2["name"].AsString.Should().Be("test-document-2");
        retrievedDocument2["value"].AsInt32.Should().Be(200);
    }
}
