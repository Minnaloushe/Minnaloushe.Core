using tik4net;

namespace Minnaloushe.Core.ClientProviders.Mikrotik;

public interface IMikrotikConnectionFactory
{
    ITikConnection Create(TikConnectionType connectionType);
}
