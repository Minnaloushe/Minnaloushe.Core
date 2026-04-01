using Telegram.Bot;

namespace Minnaloushe.Core.ClientProviders.Telegram;

public class TelegramBotClientFactory : ITelegramBotClientFactory
{
    public ITelegramBotClient Create(string botToken)
    {
        return new TelegramBotClient(botToken);
    }
}
