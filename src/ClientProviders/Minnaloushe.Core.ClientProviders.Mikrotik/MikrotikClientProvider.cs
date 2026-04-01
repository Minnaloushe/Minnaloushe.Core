using Microsoft.Extensions.Logging;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using tik4net;

namespace Minnaloushe.Core.ClientProviders.Mikrotik;

public class MikrotikClientProvider(
    IResolvedOptions<MikrotikOptions> options,
    IMikrotikConnectionFactory connectionFactory,
    ILogger<MikrotikClientProvider> logger
    )
    : IMikrotikClientProvider
{
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        if (options.IsEmpty)
        {
            logger.LogWarning("Mikrotik options configuration was not completed");
            return false;
        }
        if (options.Value.IsEmpty)
        {
            logger.LogWarning("Mikrotik options are not properly configured.");
            return false;
        }

        if (options.Value.ConnectOnStartup)
        {
            await ReOpenConnectionAsync();
        }

        return true;
    }

    public async Task ReOpenConnectionAsync()
    {
        if (Client.IsOpened)
        {
            Client.Close();
        }

        await Client.OpenAsync(options.Value.Host, options.Value.Port, options.Value.Username, options.Value.Password);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        Client.Dispose();

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    public ITikConnection Client { get; } = connectionFactory.Create(TikConnectionType.Api);
}