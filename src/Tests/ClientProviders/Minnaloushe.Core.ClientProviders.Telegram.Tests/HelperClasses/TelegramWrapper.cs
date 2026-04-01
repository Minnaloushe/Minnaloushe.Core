using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Toolbox.AsyncInitializer;

namespace Minnaloushe.Core.ClientProviders.Telegram.Tests.HelperClasses;

public class TelegramWrapper(
    ITelegramClientProvider clientProvider,
    ILogger<TelegramWrapper> logger
    ) : ITelegramWrapper, IAsyncInitializer
{
    public ITelegramClientProvider ClientProvider { get; } = clientProvider;
    public bool InitializationCompleted { get; private set; }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        if (ClientProvider.Client == null!)
        {
            logger.LogWarning("Telegram client is not initialized yet.");
            return Task.FromResult(false);
        }

        InitializationCompleted = true;

        return Task.FromResult(true);
    }
}