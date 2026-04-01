using Telegram.Bot;

namespace Minnaloushe.Core.ClientProviders.Telegram;

public interface ITelegramBotClientFactory
{
    ITelegramBotClient Create(string botToken);
}
