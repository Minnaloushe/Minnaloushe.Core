using tik4net;

namespace Minnaloushe.Core.ClientProviders.Mikrotik;

public class MikrotikConnectionFactory : IMikrotikConnectionFactory
{
    public ITikConnection Create(TikConnectionType connectionType)
    {
        return ConnectionFactory.CreateConnection(connectionType);
    }
}
