# Repository configuration usage

## Overview

The repository infrastructure provides a unified way to configure and register MongoDB and PostgreSQL database connections. It supports both static connection strings and dynamic Vault-based credential management.

## Basic setup

### 1. Registration in Program.cs

```csharp
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.MongoDb.Extensions;
using Minnaloushe.Core.Repositories.Postgres.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRepositories(builder.Configuration)
    .AddMongoDbClientProviders()
    .AddPostgresDbClientProviders()
    .Build();

var app = builder.Build();
app.Run();
```

### 2. Configuration (appsettings.json)

```json
{
  "RepositoryConfiguration": {
    "Connections": [
      {
        "Name": "mongo-default",
        "Type": "mongodb",
        "ConnectionString": "mongodb://root:example@mongodb:27017",
        "DatabaseName": "MyDatabase"
      },
      {
        "Name": "postgres-default",
        "Type": "postgres",
        "ConnectionString": "Host=postgres;Port=5432;Database=app_db;Username=user;Password=pass",
        "DatabaseName": "app_db"
      }
    ],
    "Repositories": [
      {
        "Name": "MyMongoRepository",
        "ConnectionName": "mongo-default",
        "Migrations": {
          "Enabled": false
        }
      },
      {
        "Name": "MyPostgresRepository",
        "ConnectionName": "postgres-default",
        "Migrations": {
          "Enabled": false
        }
      }
    ]
  }
}
```

## Vault-based configuration

For production environments, use Vault to manage database credentials dynamically.

### 1. Registration with Vault support

```csharp
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.MongoDb.Extensions;
using Minnaloushe.Core.Repositories.MongoDb.Vault.Extensions;
using Minnaloushe.Core.Repositories.Postgres.Extensions;
using Minnaloushe.Core.Repositories.Postgres.Vault.Extensions;
using Minnaloushe.Core.VaultService.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register Vault client provider first
builder.Services.AddVaultClientProvider();

// Register repositories with Vault support
builder.Services.AddRepositories(builder.Configuration)
    .AddMongoDbClientProviders()
    .AddVaultMongoDbClientProviders()  // Add Vault factory for MongoDB
    .AddPostgresDbClientProviders()
    .AddVaultPostgresDbClientProviders()  // Add Vault factory for PostgreSQL
    .Build();

var app = builder.Build();
app.Run();
```

### 2. Configuration with Vault (appsettings.json)

```json
{
  "Vault": {
    "ServiceName": "vault",
    "Address": "http://vault:8200",
    "Scheme": "http",
    "Token": "root",
    "MountPoint": "kv",
    "RenewalInterval": "00:01:00"
  },
  "RepositoryConfiguration": {
    "Connections": [
      {
        "Name": "mongo-vault",
        "Type": "mongodb",
        "ServiceName": "mongo",  // Service name in Vault
        "DatabaseName": "MyDatabase",
        "LeaseRenewInterval": "01:00:00"
      },
      {
        "Name": "postgres-vault",
        "Type": "postgres",
        "ServiceName": "postgres",  // Service name in Vault
        "DatabaseName": "app_db",
        "LeaseRenewInterval": "01:00:00"
      }
    ],
    "Repositories": [
      {
        "Name": "MyMongoRepository",
        "ConnectionName": "mongo-vault"
      },
      {
        "Name": "MyPostgresRepository",
        "ConnectionName": "postgres-vault"
      }
    ]
  }
}
```

## Resolving client providers at runtime

Repositories are registered as keyed services. Use the repository name to resolve the appropriate client provider:

```csharp
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.ClientProviders.Postgres;

// Resolve MongoDB client provider by repository name
var mongoProvider = app.Services.GetRequiredKeyedService<IMongoClientProvider>("MyMongoRepository");
var mongoClient = mongoProvider.Acquire();
var database = mongoClient.GetDatabase("MyDatabase");

// Resolve PostgreSQL client provider by repository name
var postgresProvider = app.Services.GetRequiredKeyedService<IPostgresClientProvider>("MyPostgresRepository");
var postgresConnection = postgresProvider.Acquire();
await using var command = postgresConnection.CreateCommand();
```

## Configuration model reference

### RepositoryConfiguration

- **Connections**: Array of connection definitions
- **Repositories**: Array of repository definitions that reference connections

### ConnectionDefinition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | Yes | Unique connection identifier |
| `Type` | string | Yes | Connection type: `"mongodb"`, `"mongo"`, `"postgres"`, or `"postgresql"` |
| `ConnectionString` | string | No | Direct connection string (excludes `ServiceName`) |
| `ServiceName` | string | No | Vault service name for dynamic credentials (excludes `ConnectionString`) |
| `DatabaseName` | string | Yes | Database name |
| `LeaseRenewInterval` | TimeSpan | No | Vault lease renewal interval (default: `01:00:00`) |
| `Parameters` | Dictionary | No | Provider-specific parameters |

### RepositoryDefinition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | Yes | Unique repository identifier (used as service key) |
| `ConnectionName` | string | Yes | Reference to a connection definition |
| `Migrations.Enabled` | bool | No | Enable database migrations (default: `false`) |

## Key points

- **Connection types**: Use `"mongodb"` or `"mongo"` for MongoDB; `"postgres"` or `"postgresql"` for PostgreSQL.
- **Credential sources**: Use either `ConnectionString` (static) or `ServiceName` (Vault-based), not both.
- **Keyed services**: Repositories are registered by their `Name` property; use `GetRequiredKeyedService<TProvider>(repositoryName)` or FromKeyedServices(repositoryName) attribute to resolve.
- **Build() required**: Always call `.Build()` to finalize registrations and create hosted services.
- **Order matters**: When using Vault, call `AddVault*ClientProviders()` after the base provider registration (`AddMongoDbClientProviders()` or `AddPostgresDbClientProviders()`).

## Provider registration workflow

1. Call `AddRepositories(configuration)` - initializes the builder
2. Call provider registration methods:
   - `AddMongoDbClientProviders()` - registers MongoDB base infrastructure
   - `AddVaultMongoDbClientProviders()` - (optional) adds Vault support for MongoDB
   - `AddPostgresDbClientProviders()` - registers PostgreSQL base infrastructure
   - `AddVaultPostgresDbClientProviders()` - (optional) adds Vault support for PostgreSQL
3. Call `.Build()` - processes configuration and registers services