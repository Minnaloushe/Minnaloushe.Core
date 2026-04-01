using DotNet.Testcontainers.Builders;
using System.Net;

namespace Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

public class VaultContainerWrapper : ContainerWrapperBase
{
    // Constants
    private const string MongoPluginName = "mongodb-database-plugin";
    private const string RolePrefix = "database-role";
    private const string PostgresPluginName = "postgresql-database-plugin";

    // Properties
    protected HttpClient HttpClient { get; private set; } = null!;
    protected override string ImageName => "hashicorp/vault:1.18.1";
    protected override ushort ContainerPort => 8200;

    public string KvMountPoint { get; private set; } = "kv";
    public string DatabaseMountPoint { get; private set; } = "database";

    public string VaultAddress { get; private set; } = string.Empty;

    // Overrides
    protected override ContainerBuilder InitContainer(ContainerBuilder builder)
        => builder.WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", Password)
            .WithEnvironment($"VAULT_DEV_LISTEN_ADDRESS", $"0.0.0.0:{ContainerPort}")
            .WithPortBinding(ContainerPort, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(req =>
                        req.ForPort(ContainerPort)
                            .ForPath("/v1/sys/health")
                            .ForStatusCode(HttpStatusCode.OK)
                        )

                );

    protected override async Task InitializeAfterStart()
    {
        VaultAddress = new UriBuilder(
            Uri.UriSchemeHttp,
            Instance.Hostname,
            Instance.GetMappedPublicPort(8200)
        ).Uri.ToString().TrimEnd('/');

        HttpClient = new HttpClient();

        HttpClient.DefaultRequestHeaders.Add("X-Vault-Token", Password);

        await InitializeVaultKvStorage();
        await InitializeVaultDatabaseEngine();
    }

    // Methods
    private async Task InitializeVaultKvStorage(string path = "kv")
    {
        KvMountPoint = path;

        var mountPayload = new
        {
            type = "kv-v2",
            description = "KV Version 2 secrets engine"
        };

        var response = await HttpClient.PostAsync(
            $"{VaultAddress}/v1/sys/mounts/{path}",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(mountPayload), System.Text.Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            if (!errorContent.Contains("path is already in use"))
            {
                throw new InvalidOperationException($"Failed to enable KV secrets engine: {errorContent}");
            }
        }
    }

    private async Task InitializeVaultDatabaseEngine(string path = "database")
    {
        DatabaseMountPoint = path;

        try
        {
            // Enable database secrets engine using HTTP API
            var mountPayload = new
            {
                type = path,
                description = "MongoDB Database Secrets Engine"
            };

            var mountResponse = await HttpClient.PostAsync(
                $"{VaultAddress}/v1/sys/mounts/{path}",
                new StringContent(System.Text.Json.JsonSerializer.Serialize(mountPayload), System.Text.Encoding.UTF8, "application/json")
            );

            if (!mountResponse.IsSuccessStatusCode)
            {
                var errorContent = await mountResponse.Content.ReadAsStringAsync();
                if (!errorContent.Contains("path is already in use"))
                {
                    throw new InvalidOperationException($"Failed to enable database secrets engine: {errorContent}");
                }
            }
        }
        catch (Exception ex) when (!ex.Message.Contains("path is already in use"))
        {
            throw new InvalidOperationException($"Failed to enable database secrets engine: {ex.Message}", ex);
        }
    }

    public async Task ConfigureStaticSecret(string secretPath, IContainerWrapper container)
    {
        var host = container.Host;
        var port = container.Port;

        var secretData = new
        {
            data = new
            {
                host,
                port,
                username = container.Username,
                password = container.Password
            }
        };

        var response = await HttpClient.PostAsync(
            $"{VaultAddress}/v1/kv/data/{secretPath}",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(secretData), System.Text.Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to store secret at '{secretPath}': {errorContent}");
        }
    }

    public async Task ConfigureStaticSecretFromObject(string secretPath, object secret)
    {
        var response = await HttpClient.PostAsync(
            $"{VaultAddress}/v1/kv/data/{secretPath}",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(secret), System.Text.Encoding.UTF8, "application/json")
        );
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to store secret at '{secretPath}': {errorContent}");
        }
    }

#pragma warning disable CA1822
    public string GetDbRoleName(IContainerWrapper container) => $"{RolePrefix}-{container.Name}";
#pragma warning restore CA1822

    public async Task ConfigureVaultMongoConnection(MongoDbContainerWrapper mongoInstance)
    {
        // Use container network alias for Vault-to-MongoDB communication
        // Vault runs inside a container and needs to use the Docker network hostname
        // Include admin credentials so Vault can manage users
        var containerNetworkUrl = $"mongodb://{mongoInstance.Username}:{mongoInstance.Password}@{mongoInstance.Name}:27017/{mongoInstance.AuthDb}";

        var configPayload = new
        {
            plugin_name = MongoPluginName,
            allowed_roles = GetDbRoleName(mongoInstance),
            connection_url = containerNetworkUrl,
            username = mongoInstance.Username,
            password = mongoInstance.Password,
            dbname = mongoInstance.AuthDb
        };

        var response = await HttpClient.PostAsync(
            $"{VaultAddress}/v1/{DatabaseMountPoint}/config/{mongoInstance.Name}",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(configPayload), System.Text.Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to configure MongoDB connection '{mongoInstance.Name}': {errorContent}");
        }

        await CreateVaultMongoRole(mongoInstance);
    }

    private async Task CreateVaultMongoRole(MongoDbContainerWrapper mongoInstance)
    {
        // MongoDB creation statement needs to specify both the target database and the admin authentication
        // Users are created in admin database but have readWrite permissions on the target database
        var creationStatement = $"{{\"db\": \"{mongoInstance.AuthDb}\", \"roles\": [{{\"role\": \"readWrite\", \"db\": \"{mongoInstance.AppDb}\"}}]}}";

        var rolePayload = new
        {
            db_name = mongoInstance.Name,
            creation_statements = new[] { creationStatement },
            default_ttl = "1h",
            max_ttl = "24h"
        };

        var response = await HttpClient.PostAsync(
            $"{VaultAddress}/v1/{DatabaseMountPoint}/roles/{GetDbRoleName(mongoInstance)}",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(rolePayload), System.Text.Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create role '{GetDbRoleName(mongoInstance)}': {errorContent}");
        }
    }

    public async Task ConfigureVaultPostgresConnection(PostgresContainerWrapper postgresInstance)
    {
        var containerNetworkUrl = $"postgresql://{{{{username}}}}:{{{{password}}}}@{postgresInstance.Name}:5432/{postgresInstance.AppDb}?sslmode=disable";

        var configPayload = new
        {
            plugin_name = PostgresPluginName,
            allowed_roles = GetDbRoleName(postgresInstance),
            connection_url = containerNetworkUrl,
            username = postgresInstance.Username,
            password = postgresInstance.Password
        };

        var response = await HttpClient.PostAsync(
            $"{VaultAddress}/v1/{DatabaseMountPoint}/config/{postgresInstance.Name}",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(configPayload), System.Text.Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to configure PostgreSQL connection '{postgresInstance.Name}': {errorContent}");
        }

        await CreateVaultPostgresRole(postgresInstance);
    }

    private async Task CreateVaultPostgresRole(PostgresContainerWrapper postgresInstance)
    {
        var creationStatement = $@"
            CREATE ROLE ""{{{{name}}}}"" WITH LOGIN PASSWORD '{{{{password}}}}' VALID UNTIL '{{{{expiration}}}}';
            GRANT CONNECT ON DATABASE ""{postgresInstance.AppDb}"" TO ""{{{{name}}}}"";
            GRANT USAGE ON SCHEMA public TO ""{{{{name}}}}"";
            GRANT CREATE ON SCHEMA public TO ""{{{{name}}}}"";
            GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO ""{{{{name}}}}"";
            GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO ""{{{{name}}}}"";
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL PRIVILEGES ON TABLES TO ""{{{{name}}}}"";
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL PRIVILEGES ON SEQUENCES TO ""{{{{name}}}}"";";

        var rolePayload = new
        {
            db_name = postgresInstance.Name,
            creation_statements = new[] { creationStatement },
            default_ttl = "1h",
            max_ttl = "24h"
        };

        var response = await HttpClient.PostAsync(
            $"{VaultAddress}/v1/{DatabaseMountPoint}/roles/{GetDbRoleName(postgresInstance)}",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(rolePayload), System.Text.Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create role '{postgresInstance.Name}': {errorContent}");
        }
    }

    // Dispose
    public override async ValueTask DisposeAsync()
    {
        HttpClient.Dispose();

        await base.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}