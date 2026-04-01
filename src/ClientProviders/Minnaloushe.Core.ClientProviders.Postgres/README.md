# Minnaloushe.Core.ClientProviders.Postgres

PostgreSQL client provider implementation following the same pattern as MongoDB client provider.

## Features

- **Vault-based Provider**: Dynamic credential management using HashiCorp Vault
- **Connection String Provider**: Static connection string from configuration
- **Factory Pattern**: Extensible factory selection based on configuration
- **Automatic Provider Selection**: Determines provider type based on configuration
- **Keyed Services**: Repository-level and connection-level service registration

## Usage

### 1. Register PostgreSQL Client Providers

```csharp
services.AddPostgresDbClientProviders();
services.ConfigurePostgresDbRepositories(configuration);
```

### 2. Configuration Examples

#### Using Vault (Dynamic Credentials)

```json
{
  "RepositoryConfiguration": {
    "Connections": [
      {
        "Name": "vault-postgres",
        "Type": "postgres",
        "ServiceName": "postgres-service"
      }
    ],
    "Repositories": [
      {
        "Name": "MyRepository",
        "ConnectionName": "vault-postgres",
        "DatabaseName": "mydb"
      }
    ]
  }
}
```

#### Using Connection String

```json
{
  "RepositoryConfiguration": {
    "Connections": [
      {
        "Name": "direct-postgres",
        "Type": "postgresql",
        "ConnectionString": "Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass"
      }
    ],
    "Repositories": [
      {
        "Name": "MyRepository",
        "ConnectionName": "direct-postgres",
        "DatabaseName": "mydb"
      }
    ]
  }
}
```

### 3. Inject PostgreSQL Client Provider

```csharp
public class MyRepository
{
    private readonly IPostgresClientProvider _clientProvider;
    
    public MyRepository(
        [FromKeyedServices("MyRepository")] IPostgresClientProvider clientProvider)
    {
        _clientProvider = clientProvider;
    }
    
    public async Task DoSomethingAsync()
    {
        using var lease = _clientProvider.Acquire();
        using var connection = lease.Client;
        
        await connection.OpenAsync();
        // Use connection...
    }
}
```

## Architecture

### Core Components

1. **IPostgresClientProvider**: Provider interface for `NpgsqlDataSource`
2. **PostgresClientProvider**: Vault-based provider with credential rotation
3. **ConnectionStringPostgresClientProvider**: Simple connection string provider
4. **IPostgresClientProviderFactory**: Factory interface with `CanCreate` method
5. **IPostgresClientProviderFactorySelector**: Selects appropriate factory

### Factory Selection

Factories are evaluated in registration order:
1. **VaultPostgresClientProviderFactory**: Matches when `ServiceName` is configured
2. **ConnectionStringPostgresClientProviderFactory**: Matches when `ConnectionString` is configured

### Extension Points

Register custom factories:

```csharp
public class AzureKeyVaultPostgresClientProviderFactory : IPostgresClientProviderFactory
{
    public bool CanCreate(RepositoryOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.KeyVaultName);
    }
    
    public IPostgresClientProvider Create(string connectionName)
    {
        // Custom implementation
    }
}

// Register before default factories
services.AddSingleton<IPostgresClientProviderFactory, AzureKeyVaultPostgresClientProviderFactory>();
services.AddPostgresDbClientProviders();
```

## Connection Types

Supports both:
- `"Type": "postgres"`
- `"Type": "postgresql"`

## Dependencies

- `Npgsql` (10.0.1+)
- `Minnaloushe.Core.ClientProviders.Abstractions`
- `Minnaloushe.Core.Common`
- `Minnaloushe.Core.ServiceDiscovery`
- `Minnaloushe.Core.VaultService`
- `System.Reactive`
