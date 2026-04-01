using Minnaloushe.Core.ClientProviders.Abstractions.StaticClientProvider;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Telegram.Bot;

namespace Minnaloushe.Core.ClientProviders.Telegram;

public interface ITelegramClientProvider : IStaticClientProvider<ITelegramBotClient>, IAsyncInitializer
{
    long ChatId { get; }
};