using Microsoft.Extensions.Logging;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using Telegram.Bot;

namespace Minnaloushe.Core.ClientProviders.Telegram;

public class TelegramClientProvider(
    IResolvedOptions<TelegramOptions> options,
    ITelegramBotClientFactory factory,
    ILogger<TelegramClientProvider> logger)
    : ITelegramClientProvider
{
    private ITelegramBotClient? _client;

    public Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        if (options.IsEmpty)
        {
            logger.LogWarning("Telegram options configuration was not completed");
            return Task.FromResult(false);
        }
        if (options.Value.IsEmpty)
        {
            logger.LogWarning("Telegram options are not properly configured.");
            return Task.FromResult(false);
        }

        // If already initialized, don't create another client (avoids duplicate factory calls)
        if (_client is not null)
        {
            return Task.FromResult(true);
        }

        _client = factory.Create(options.Value.BotToken);
        ChatId = options.Value.ChatId;

        return Task.FromResult(true);
    }

    public long ChatId { get; private set; }

    public ITelegramBotClient Client => _client ?? throw new InvalidOperationException("Telegram client has not been initialized yet.");
}
