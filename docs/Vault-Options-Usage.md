# Vault options usage

## Overview

The Vault options infrastructure enables loading configuration from HashiCorp Vault's KV v2 secret store. Options are loaded during application startup via async initializers and accessed synchronously through `IResolvedOptions<T>.Value`.

## Basic setup

### 1. Define your options class

Create a record that inherits from `VaultStoredOptions`:

```csharp
using Minnaloushe.Core.VaultOptions.Vault;

public record MyServiceOptions : VaultStoredOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string ServiceUrl { get; init; } = string.Empty;
    public int Timeout { get; init; } = 30;

    // Required: define when options are considered empty
    public override bool IsEmpty => 
        ApiKey.IsNullOrWhiteSpace() || ServiceUrl.IsNullOrWhiteSpace();

    // Required: define how to merge Vault data with existing config
    public override VaultStoredOptions ApplyVaultData(IDictionary<string, object> vaultData)
    {
        return this with
        {
            ApiKey = vaultData.GetStringValue(nameof(ApiKey)) ?? ApiKey,
            ServiceUrl = vaultData.GetStringValue(nameof(ServiceUrl)) ?? ServiceUrl,
            Timeout = vaultData.GetIntValue(nameof(Timeout)) ?? Timeout
        };
    }
}
```

### 2. Register services in Program.cs

```csharp
using Minnaloushe.Core.VaultService.Extensions;
using Minnaloushe.Core.VaultOptions.Extensions;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure async initializers (required for Vault)
builder.Services.ConfigureAsyncInitializers();

// Register Vault services
builder.Services.AddVaultClientProvider();
builder.Services.AddVaultStoredOptions();

var app = builder.Build();

// Invoke async initializers before running the app (loads Vault options)
await app.InvokeAsyncInitializers();

await app.RunAsync();
```

### 3. Configure in appsettings.json

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
  "MyService": {
    "VaultPath": "my-service-credentials", // Relative path in KV will be prefixed to /{App_Namespace}/my-service-credentials
    "ServiceUrl": "https://fallback.example.com",  // Optional: fallback if Vault unavailable
    "Timeout": 60  // Can be overridden by Vault or used as default
  },
  "MyAnotherService": {
    "VaultPath": "/my-another-service-credentials", // With leading slash it is considered absolute path and used as is
    "ServiceUrl": "https://fallback.example.com",  // Optional: fallback if Vault unavailable
    "Timeout": 60  // Can be overridden by Vault or used as default
  }  
}
```

### 4. Use options in your code

Inject `IResolvedOptions<T>` and access the `.Value` property. Options are loaded during startup by async initializers:

```csharp
using Minnaloushe.Core.VaultOptions.ResolvedOptions;

public class MyService
{
    private readonly IResolvedOptions<MyServiceOptions> _options;
    
    public MyService(IResolvedOptions<MyServiceOptions> options)
    {
        _options = options;
    }
    
    public void DoWork()
    {
        // Access options synchronously - already loaded from Vault during startup
        var apiKey = _options.Value.ApiKey;
        var serviceUrl = _options.Value.ServiceUrl;
        
        // Use the options
        Console.WriteLine($"Connecting to {serviceUrl}");
    }
}
```

## Keyed options

For managing multiple instances of the same options type with different keys:

### 1. Configure multiple instances

```json
{
  "Vault": {
    "ServiceName": "vault",
    "Address": "http://vault:8200",
    "Token": "root",
    "MountPoint": "kv"
  },
  "ExternalApis": {
    "ServiceA": {
      "VaultPath": "external-api-service-a",
      "ServiceUrl": "https://servicea.example.com"
    },
    "ServiceB": {
      "VaultPath": "external-api-service-b",
      "ServiceUrl": "https://serviceb.example.com"
    }
  }
}
```

### 2. Use keyed resolved options

```csharp
using Minnaloushe.Core.VaultOptions.ResolvedOptions;

public class ApiAggregator
{
    private readonly IResolvedKeyedOptions<ExternalApiOptions> _options;
    
    public ApiAggregator(IResolvedKeyedOptions<ExternalApiOptions> options)
    {
        _options = options;
    }
    
    public async Task CallServiceAAsync()
    {
        // Get options by key (async method for keyed options)
        var options = await _options.GetOptionsAsync("ServiceA");
        
        // Use ServiceA credentials
        var apiKey = options.ApiKey;
    }
    
    public async Task CallServiceBAsync()
    {
        var options = await _options.GetOptionsAsync("ServiceB");
        
        // Use ServiceB credentials
        var apiKey = options.ApiKey;
    }
}
```

## Defining custom options

Your options class must inherit from `VaultStoredOptions` and implement two members:

### Required implementations

**IsEmpty** - Returns `true` when critical properties are missing:
```csharp
public override bool IsEmpty => 
    ApiKey.IsNullOrWhiteSpace() || ServiceUrl.IsNullOrWhiteSpace();
```

**ApplyVaultData** - Merges Vault data into the options:
```csharp
public override VaultStoredOptions ApplyVaultData(IDictionary<string, object> vaultData)
{
    return this with
    {
        ApiKey = vaultData.GetStringValue(nameof(ApiKey)) ?? ApiKey,
        ServiceUrl = vaultData.GetStringValue(nameof(ServiceUrl)) ?? ServiceUrl,
        Timeout = vaultData.GetIntValue(nameof(Timeout)) ?? Timeout
    };
}
```

Helper methods: `GetStringValue()`, `GetIntValue()`, `GetLongValue()`, `GetBoolValue()` from `Minnaloushe.Core.Toolbox.DictionaryExtensions`.

## Vault configuration (appsettings.json)

```json
{
  "Vault": {
    "ServiceName": "vault",
    "Address": "http://vault:8200",
    "Scheme": "http",
    "Token": "root",
    "MountPoint": "kv",
    "RenewalInterval": "00:01:00"
  }
}
```

Key properties:
- **Address**: Vault server URL with scheme and port
- **Token**: Vault authentication token
- **MountPoint**: KV v2 mount point (default: `"kv"`)
- **RenewalInterval**: Token renewal interval (default: `00:01:00`)

## How it works

1. During application startup, `InvokeAsyncInitializers()` triggers Vault loading
2. If `IsEmpty` returns `true`, secrets are loaded from the configured `VaultPath`
3. `ApplyVaultData()` merges Vault values with config file values
4. Options are stored in `IResolvedOptions<T>` for synchronous access
5. Your code accesses loaded options via `.Value` property

## Key points
- IMPORTANT **Application startup**: If Vault service is unavailable or options were not fully loaded (IsEmpty == false), application initialization will be considered as failed and app won't start.
- **Async initializers required**: Call `ConfigureAsyncInitializers()` during setup and `InvokeAsyncInitializers()` before running the app
- **Synchronous access**: Use `IResolvedOptions<T>.Value` (property, not async method) after startup
- **Keyed options**: `IResolvedKeyedOptions<T>.GetOptionsAsync(key)` added to support keyed client providers like telegram
- **VaultPath**: Set in appsettings.json to specify the secret location in Vault's KV v2 store. Supports absolute/relative paths, in case of relative path it will be prefixed with app namespace
- **Fallback values**: Config file values serve as defaults if Vault does not have value for that key
- **Loading happens at startup**: Options are loaded during `InvokeAsyncInitializers()`, not on first access.

## Example implementations

Check these examples in the codebase:

- **S3StorageOptions**: [Minnaloushe.Core.ClientProviders.Minio/Options/S3StorageOptions.cs](../src/ClientProviders/Minnaloushe.Core.ClientProviders.Minio/Options/S3StorageOptions.cs)
- **TelegramOptions**: [Minnaloushe.Core.ClientProviders.Telegram/TelegramOptions.cs](../src/ClientProviders/Minnaloushe.Core.ClientProviders.Telegram/TelegramOptions.cs)
