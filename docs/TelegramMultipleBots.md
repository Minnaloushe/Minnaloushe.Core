# Telegram Multiple Bots Support

## Overview

The Telegram client provider has been refactored to support multiple bot configurations using keyed service registration. Each bot can have its own configuration and can be accessed independently through a single async initializer that processes the entire Telegram section.

## Configuration Structure

The new configuration structure under `appsettings.json`:

```json
{
  "Telegram": {
    "ReporterBot": {
      "BotToken": "your-reporter-bot-token",
      "ChatId": "-1001234567890",
      "VaultPath": "ReporterBot"
    },
    "AnotherBot": {
      "BotToken": "your-another-bot-token",
      "ChatId": "-1009876543210",
      "VaultPath": "AnotherBot"
    }
  }
}
```

### Configuration Properties

- **BotToken**: The Telegram Bot API token (required)
- **ChatId**: The default chat ID for the bot (optional, defaults to 0)
- **VaultPath**: The path in Vault where additional secrets are stored (optional)

## Registration

### Automatic Registration

In your `Program.cs`, register all Telegram bots defined in configuration:

```csharp
builder.Services.AddTelegramClientProviders(builder.Configuration);
```

This method automatically:
1. Reads all bot configurations under the "Telegram" section
2. Registers each bot as a keyed service using its configuration key (e.g., "ReporterBot", "AnotherBot")
3. Creates a single `TelegramClientProvidersInitializer` that processes all bots
4. Sets up async initialization for all bots through one initializer
5. Configures vault integration if VaultPath is specified for each bot

## Architecture

### Single Async Initializer Pattern

Unlike other client providers, the Telegram provider uses a single async initializer (`TelegramClientProvidersInitializer`) that processes all bots in the Telegram section. This is necessary because keyed services cannot be registered as individual async initializers.

**Key Implementation Details:**

- `TelegramClientProvidersInitializer` maintains a list of all registered bot keys
- During initialization, it iterates through each bot key and:
  - Loads configuration from IOptionsMonitor
  - Applies Vault secrets if VaultPath is configured
  - Stores resolved options in `ResolvedKeyedOptions<TelegramOptions>`
  - Resolves the keyed provider instance
  - Calls `InitializeAsync` on each provider
- All bots are initialized sequentially with comprehensive logging

## Usage

### Using IServiceProvider

You can inject specific bots by key using `IServiceProvider`:

```csharp
[ApiController]
[Route("api/[controller]")]
public class TelegramController(IServiceProvider serviceProvider) : ControllerBase
{
    [HttpPost("send/{botKey}")]
    public async Task<IActionResult> SendMessage(
        string botKey,
        string message,
        CancellationToken cancellationToken)
    {
        // Get the specific bot by key
        var telegramClient = serviceProvider.GetRequiredKeyedService<ITelegramClientProvider>(botKey);
        var options = serviceProvider.GetRequiredKeyedService<IResolvedOptions<TelegramOptions>>(botKey);
        
        await telegramClient.Client.SendMessage(
            chatId: options.Value.ChatId,
            text: message,
            cancellationToken: cancellationToken);
        
        return Ok();
    }
}
```

### Using constructor injection

Similarly, in your services:

```csharp
public class MyService
{
    private readonly ITelegramClientProvider _reporterBot;
    private readonly ITelegramClientProvider _anotherBot;
    
    private const string ReporterBotKey = "ReporterBot";
    private const string AnotherBotKey = "AnotherBot";

    public MyService(
        [FromKeyedServices(ReporterBotKey) reporterBot]
        [FromKeyedServices(AnotherBotKey) anotherBot]
    )
    {
        _reporterBot = reporterBot;
        _anotherBot = anotherBot;
    }
    
    public async Task SendReportAsync(string message, CancellationToken cancellationToken)
    {
        await _reporterBot.Client.SendMessage(
            reporterBot.ChatId, 
            message, 
            cancellationToken: cancellationToken);
    }
}
```

## Implementation Details

### Key Components

1. **TelegramOptions**: Configuration model with VaultPath support for keyed options
2. **TelegramClientProvider**: Implements `ITelegramClientProvider` with async initialization
3. **TelegramClientProvidersInitializer**: Single async initializer that processes all bots in the Telegram section
4. **AddTelegramClientProviders**: Enumerates configuration and registers all bots as keyed services

### Initialization Flow

1. `AddTelegramClientProviders` reads all child sections under "Telegram"
2. For each bot key:
   - Configuration is bound to `TelegramOptions` with the key
   - Provider is registered as a keyed singleton
   - Keyed `IResolvedOptions<TelegramOptions>` is registered
3. `TelegramClientProvidersInitializer` is created with all bot keys registered
4. During app startup, the initializer:
   - Loads options for each bot (with Vault integration)
   - Calls `InitializeAsync` on each bot's provider
   - Logs initialization progress for each bot

### Logging

The initializer provides structured logging using source-generated logger messages:
- `LogInitializingTelegramBots`: Information level, at start
- `LogInitializingBot`: Debug level, for each bot
- `LogBotInitialized`: Debug level, on success
- `LogBotInitializationFailed`: Error level, on failure
- `LogAllBotsInitialized`: Information level, at completion

## Migration from Single Bot

If you were using the old single-bot approach:

**Old:**
```csharp
builder.Services.AddTelegramClientProvider();

// In controller
public MyController(ITelegramClientProvider telegramClient) { }
```

**New:**
```csharp
builder.Services.AddKeyedTelegramClientProviders(builder.Configuration);

// In controller
public MyController(IServiceProvider serviceProvider)
{
    var telegramClient = serviceProvider.GetRequiredKeyedService<ITelegramClientProvider>("BotKey");
}
```

## Error Handling

The provider throws `InvalidOperationException` in the following cases:
- Options configuration was not completed (vault loading failed)
- Options are not properly configured (missing BotToken)
- Client accessed before initialization
- Requested bot key doesn't exist in configuration

During initialization, if any bot fails to initialize, the exception is logged and re-thrown, preventing application startup.

## Notes

- Bot keys are case-sensitive
- All bots are initialized sequentially during application startup
- Each bot maintains its own `TelegramBotClient` instance
- The ChatId in options is optional and can be overridden when sending messages
- The single initializer pattern ensures proper error handling and logging for all bots
- Keyed services require explicit resolution via `GetRequiredKeyedService` with the bot key
