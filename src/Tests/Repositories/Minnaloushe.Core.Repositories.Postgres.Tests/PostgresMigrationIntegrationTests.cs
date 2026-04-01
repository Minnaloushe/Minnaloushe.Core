using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.Migrations.Postgres;
using Minnaloushe.Core.Repositories.Postgres.Extensions;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Npgsql;

namespace Minnaloushe.Core.Repositories.Postgres.Tests;

[Category("Integration")]
[Category("TestContainers")]
public class PostgresMigrationIntegrationTests
{
    #region Constants

    private const string ConnectionName = "postgres-connection";
    private const string RepositoryName = "postgres-repository";

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

                services.AddPostgresMigration<InitialMigration>();
                services.AddPostgresMigration<AddIndexMigration>();

                services.AddRepositories(configuration)
                    .AddPostgresDbClientProviders()
                    .AddPostgresMigrations()
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
                        Type = "postgres",
                        ConnectionString =
                            $"Host={GlobalFixture.Postgres.Host};Port={GlobalFixture.Postgres.Port};" +
                            $"Username={GlobalFixture.Postgres.Username};Password={GlobalFixture.Postgres.Password};" +
                            $"Database={GlobalFixture.Postgres.AppDb}",
                        DatabaseName = GlobalFixture.Postgres.AppDb
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

    private IPostgresClientProvider GetClientProvider()
    {
        var clientProvider = _sut.Services.GetRequiredKeyedService<IPostgresClientProvider>(ConnectionName);

        return clientProvider;
    }

    #endregion

    [Test]
    public void WhenResolvingRepositoryOptions_ThenShouldNotBeNull()
    {
        // Arrange
        using var scope = _sut.Services.CreateScope();

        // Act
        var options = scope.ServiceProvider.GetService<IOptionsMonitor<RepositoryOptions>>();
        var value = options?.Get(RepositoryName);

        // Assert
        using var assertionScope = new AssertionScope();
        options.Should().NotBeNull("repository options should be registered");
        value.Should().NotBeNull();
        value!.ConnectionName.Should().Be(ConnectionName, "repository options should have the correct connection name");
        value.DatabaseName.Should().Be(GlobalFixture.Postgres.AppDb, "repository options should have the correct database name");
    }

    [Test]
    public void WhenResolvingProviderByConnectionName_ThenShouldNotBeNull()
    {
        // Arrange & Act
        var provider = _sut.Services.GetKeyedService<IPostgresClientProvider>(ConnectionName);

        // Assert
        provider.Should().NotBeNull("PostgreSQL client provider should be registered for the connection");
    }

    [Test]
    public void WhenResolvingProviderByRepositoryAndConnectionName_ThenShouldReturnSameInstance()
    {
        // Arrange & Act
        var providerByConnection = _sut.Services.GetKeyedService<IPostgresClientProvider>(ConnectionName);
        var providerByRepository = _sut.Services.GetKeyedService<IPostgresClientProvider>(RepositoryName);

        // Assert
        using var assertionScope = new AssertionScope();
        providerByConnection.Should().NotBeNull();
        providerByRepository.Should().Be(providerByConnection, "the same client provider should be resolved by both connection name and repository name");
    }

    [Test]
    public async Task InitialMigration_WhenExecuted_ThenTestItemsTableExists()
    {
        // Arrange
        var clientProvider = GetClientProvider();
        using var lease = clientProvider.Acquire();

        // Act

        await using var command = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'test_items')",
            lease.Client);
        var exists = (bool)(await command.ExecuteScalarAsync())!;

        // Assert
        exists.Should().BeTrue("the test_items table should have been created by InitialMigration");
    }

    [Test]
    public async Task AddIndexMigration_WhenExecuted_ThenUniqueIndexOnUniqueKeyExists()
    {
        // Arrange
        var clientProvider = GetClientProvider();
        using var lease = clientProvider.Acquire();

        // Act
        var connection = GetClientProvider();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND indexname = 'ix_test_items_unique_key')",
            lease.Client);
        var exists = (bool)(await command.ExecuteScalarAsync())!;

        // Assert
        exists.Should().BeTrue("the unique index should have been created by AddIndexMigration");
    }

    [Test]
    public async Task Migrations_WhenApplied_ThenTrackedInMigrationsTable()
    {
        // Arrange
        var clientProvider = GetClientProvider();
        using var lease = clientProvider.Acquire();

        // Act
        await using var command = new NpgsqlCommand("SELECT id FROM __migrations ORDER BY id", lease.Client);
        await using var reader = await command.ExecuteReaderAsync();

        var ids = new List<string>();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        // Assert
        using var assertionScope = new AssertionScope();
        ids.Should().Contain("01_InitialMigration");
        ids.Should().Contain("02_AddIndexMigration");
    }

    [Test]
    public async Task Migrations_WhenAppliedTwice_ThenNotReapplied()
    {
        // Arrange
        await using var secondHost = await TestHost.Build(
            configureConfiguration: cfg => cfg.AddConfiguration(AppSettings),
            configureServices: (services, configuration) =>
            {
                services.AddLogging(builder => builder.AddConsole());
                services.AddSingleton(configuration);
                services.ConfigureAsyncInitializers();

                services.AddPostgresMigration<InitialMigration>();
                services.AddPostgresMigration<AddIndexMigration>();

                services.AddRepositories(configuration)
                    .AddPostgresDbClientProviders()
                    .AddPostgresMigrations()
                    .Build();
            },
            beforeStart: async host => { await host.InvokeAsyncInitializers(); },
            startHost: true);

        var clientProvider = GetClientProvider();
        using var lease = clientProvider.Acquire();

        // Act
        var connection = GetClientProvider();
        await using var command = new NpgsqlCommand("SELECT COUNT(1) FROM __migrations", lease.Client);
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());

        // Assert
        count.Should().Be(2, "each migration should be applied exactly once even when host starts twice");
    }

    #region Helper Classes

    internal class InitialMigration(
        [FromKeyedServices(RepositoryName)] IPostgresClientProvider clientProvider) : IPostgresMigration
    {
        public string Id => "01_InitialMigration";
        public string TargetRepository => RepositoryName;

        public async Task UpAsync(CancellationToken cancellationToken)
        {
            using var lease = clientProvider.Acquire();

            await using var command = new NpgsqlCommand(
                "CREATE TABLE test_items (id SERIAL PRIMARY KEY, unique_key UUID NOT NULL, data TEXT NOT NULL)",
                lease.Client);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    internal class AddIndexMigration(
        [FromKeyedServices(RepositoryName)] IPostgresClientProvider clientProvider) : IPostgresMigration
    {
        public string Id => "02_AddIndexMigration";
        public string TargetRepository => RepositoryName;

        public async Task UpAsync(CancellationToken cancellationToken)
        {
            using var lease = clientProvider.Acquire();
            await using var command = new NpgsqlCommand(
                "CREATE UNIQUE INDEX ix_test_items_unique_key ON test_items (unique_key)",
                lease.Client);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    #endregion
}
