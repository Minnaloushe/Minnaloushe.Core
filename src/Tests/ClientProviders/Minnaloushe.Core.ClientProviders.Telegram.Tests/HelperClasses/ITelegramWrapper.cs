namespace Minnaloushe.Core.ClientProviders.Telegram.Tests.HelperClasses;

public interface ITelegramWrapper
{
    ITelegramClientProvider ClientProvider { get; }
    bool InitializationCompleted { get; }
}