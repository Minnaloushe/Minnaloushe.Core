using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.Postgres.Extensions;
using Minnaloushe.Core.Repositories.Postgres.Vault.Extensions;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Entities;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Minnaloushe.Core.VaultService.Extensions;
using Moq;

namespace Minnaloushe.Core.ClientProviders.Postgre.Tests.Integration;

[Category("Integration")]
[Category("TestContainers")]
public class PostgresVaultIntegrationTests
{
    #region Constants

    private const string RepositoryName1 = "vault-repo-1";
    private const string RepositoryName2 = "vault-repo-2";
    private const string ConnectionName1 = "vault-postgres-connection-1";
    private const string ConnectionName2 = "vault-postgres-connection-2";

    #endregion

    private TestHost _sut = null!;
    private Mock<IServiceDiscoveryService> _mockServiceDiscovery = null!;
    private Mock<IInfrastructureConventionProvider> _mockDependenciesProvider = null!;

    private static object AppSettings =>
        new
        {
            Vault = new
            {
                Address = GlobalFixture.Vault.VaultAddress,
                Token = GlobalFixture.Vault.Password,
                Scheme = "http",
                ServiceName = GlobalFixture.Vault.Name
            },
            ServiceDiscovery = new
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
                        Type = "postgres",
                        ServiceName = GlobalFixture.Postgres1.Name,
                        LeaseRenewInterval = "00:00:30",
                        DatabaseName = GlobalFixture.Postgres1.AppDb
                    },
                    new
                    {
                        Name = ConnectionName2,
                        Type = "postgres",
                        ServiceName = GlobalFixture.Postgres2.Name,
                        LeaseRenewInterval = "00:00:30",
                        DatabaseName = GlobalFixture.Postgres2.AppDb
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

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        #region Prepare mocks

        _mockServiceDiscovery = new Mock<IServiceDiscoveryService>();
        _mockServiceDiscovery
            .Setup(x => x.ResolveServiceEndpoint(
                GlobalFixture.Postgres1.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ServiceEndpoint
            {
                Host = GlobalFixture.Postgres1.Host,
                Port = GlobalFixture.Postgres1.Port
            }]);
        _mockServiceDiscovery
            .Setup(x => x.ResolveServiceEndpoint(
                GlobalFixture.Postgres2.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ServiceEndpoint
            {
                Host = GlobalFixture.Postgres2.Host,
                Port = GlobalFixture.Postgres2.Port
            }]);

        _mockDependenciesProvider = new Mock<IInfrastructureConventionProvider>();
        _mockDependenciesProvider
            .Setup(x => x.GetDatabaseRole(GlobalFixture.Postgres1.Name, It.IsAny<string>()))
            .ReturnsAsync(GlobalFixture.Vault.GetDbRoleName(GlobalFixture.Postgres1));
        _mockDependenciesProvider
            .Setup(x => x.GetDatabaseRole(GlobalFixture.Postgres2.Name, It.IsAny<string>()))
            .ReturnsAsync(GlobalFixture.Vault.GetDbRoleName(GlobalFixture.Postgres2));
        _mockDependenciesProvider
            .Setup(x => x.GetConsulServiceName(GlobalFixture.Postgres1.Name))
            .ReturnsAsync(GlobalFixture.Postgres1.Name);
        _mockDependenciesProvider
            .Setup(x => x.GetConsulServiceName(GlobalFixture.Postgres2.Name))
            .ReturnsAsync(GlobalFixture.Postgres2.Name);

        #endregion

        #region Build TestHost

        _sut = await TestHost.Build(
            configureConfiguration: cfg =>
            {
                cfg.AddConfiguration(AppSettings);
            },
            configureServices: (services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                services.AddSingleton(configuration);
                services.AddSingleton(_mockServiceDiscovery.Object);
                services.AddSingleton(_mockDependenciesProvider.Object);

                services.ConfigureAsyncInitializers();

                services.AddVaultClientProvider();

                services.AddRepositories(configuration)
                    .AddPostgresDbClientProviders()
                    .AddVaultPostgresDbClientProviders()
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

    [Test]
    public void WhenResolvingFirstProviderByConnectionNameProvider1ShouldNotBeNull()
    {
        var providerByConnection1 = _sut.Services.GetKeyedService<IPostgresClientProvider>(ConnectionName1);
        providerByConnection1.Should().NotBeNull("First PostgreSQL client provider should be registered");
    }

    [Test]
    public void WhenResolvingSecondProviderByConnectionNameProvider2ShouldNotBeNull()
    {
        var providerByConnection2 = _sut.Services.GetKeyedService<IPostgresClientProvider>(ConnectionName2);
        providerByConnection2.Should().NotBeNull("Second PostgreSQL client provider should be registered");
    }

    [Test]
    public void WhenResolvingFirstProviderByConnectionAndRepositoryNameThenShouldReturnSameInstance()
    {
        var providerByConnection1 = _sut.Services.GetKeyedService<IPostgresClientProvider>(ConnectionName1);
        var providerByRepository1 = _sut.Services.GetKeyedService<IPostgresClientProvider>(RepositoryName1);
        providerByConnection1.Should().Be(providerByRepository1, "For first connection same client provider should be resolved by connection name and repository name");
    }

    [Test]
    public void WhenResolvingSecondProviderByConnectionAndRepositoryNameThenShouldReturnSameInstance()
    {
        var providerByConnection2 = _sut.Services.GetKeyedService<IPostgresClientProvider>(ConnectionName2);
        var providerByRepository2 = _sut.Services.GetKeyedService<IPostgresClientProvider>(RepositoryName2);
        providerByConnection2.Should().Be(providerByRepository2, "For second connection same client provider should be resolved by connection name and repository name");
    }

    [Test]
    public void WhenResolvingClientProvidersThenShouldNotReturnSameInstanceForFirstAndSecondConnection()
    {
        var providerByConnection1 = _sut.Services.GetKeyedService<IPostgresClientProvider>(ConnectionName1);
        var providerByConnection2 = _sut.Services.GetKeyedService<IPostgresClientProvider>(ConnectionName2);

        providerByConnection1.Should().NotBe(providerByConnection2, "Resolved providers should not reference same instance");
    }

    [Test]
    public void WhenResolvingClientProvidersThenProvidersShouldReferenceDifferentHosts()
    {
        var providerByConnection1 = _sut.Services.GetRequiredKeyedService<IPostgresClientProvider>(ConnectionName1);
        var providerByConnection2 = _sut.Services.GetRequiredKeyedService<IPostgresClientProvider>(ConnectionName2);

        using var lease1 = providerByConnection1.Acquire();
        using var lease2 = providerByConnection2.Acquire();

        lease1.Client.ConnectionString.Should().NotBe(lease2.Client.ConnectionString, "Providers should reference different hosts");
    }

    [Test]
    public async Task WhenAccessingDatabase1ThenShouldInsertAndRetrieveData()
    {
        var postgresProvider1 = _sut.Services.GetRequiredKeyedService<IPostgresClientProvider>(ConnectionName1);
        using var dataSourceLease1 = postgresProvider1.Acquire();
        var connection = dataSourceLease1.Client;


        await using var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS test_table_1 (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                connection TEXT NOT NULL,
                timestamp TIMESTAMP NOT NULL,
                value INTEGER NOT NULL
            )";
        await createTableCmd.ExecuteNonQueryAsync();

        await using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO test_table_1 (name, connection, timestamp, value)
            VALUES (@name, @connection, @timestamp, @value)
            RETURNING id";
        insertCmd.Parameters.AddWithValue("name", "test-document-1");
        insertCmd.Parameters.AddWithValue("connection", ConnectionName1);
        insertCmd.Parameters.AddWithValue("timestamp", DateTime.UtcNow);
        insertCmd.Parameters.AddWithValue("value", 100);

        await insertCmd.ExecuteScalarAsync();

        await using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT name, value FROM test_table_1 WHERE name = @name";
        selectCmd.Parameters.AddWithValue("name", "test-document-1");

        await using var reader = await selectCmd.ExecuteReaderAsync();
        reader.Read().Should().BeTrue("Document should be retrieved from first PostgreSQL");
        reader.GetString(0).Should().Be("test-document-1");
        reader.GetInt32(1).Should().Be(100);
    }

    [Test]
    public async Task WhenAccessingDatabase2ThenShouldInsertAndRetrieveData()
    {
        var postgresProvider2 = _sut.Services.GetRequiredKeyedService<IPostgresClientProvider>(ConnectionName2);
        using var dataSourceLease2 = postgresProvider2.Acquire();
        var connection = dataSourceLease2.Client;

        await using var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS test_table_2 (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                connection TEXT NOT NULL,
                timestamp TIMESTAMP NOT NULL,
                value INTEGER NOT NULL
            )";
        await createTableCmd.ExecuteNonQueryAsync();

        await using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO test_table_2 (name, connection, timestamp, value)
            VALUES (@name, @connection, @timestamp, @value)
            RETURNING id";
        insertCmd.Parameters.AddWithValue("name", "test-document-2");
        insertCmd.Parameters.AddWithValue("connection", ConnectionName2);
        insertCmd.Parameters.AddWithValue("timestamp", DateTime.UtcNow);
        insertCmd.Parameters.AddWithValue("value", 200);

        await insertCmd.ExecuteScalarAsync();

        await using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT name, value FROM test_table_2 WHERE name = @name";
        selectCmd.Parameters.AddWithValue("name", "test-document-2");

        await using var reader = await selectCmd.ExecuteReaderAsync();
        reader.Read().Should().BeTrue("Document should be retrieved from second PostgreSQL");
        reader.GetString(0).Should().Be("test-document-2");
        reader.GetInt32(1).Should().Be(200);
    }
}
