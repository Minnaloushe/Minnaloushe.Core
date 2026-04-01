namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

/// <summary>
/// Default implementation of connection type registry.
/// Maintains connection types grouped by provider (e.g., all Kafka variants, all RabbitMQ variants).
/// </summary>
public class ConnectionTypeRegistry : IConnectionTypeRegistry
{
    private readonly HashSet<string> _connectionTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _groupedConnectionTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <inheritdoc/>
    public void RegisterConnectionType(string connectionType, string providerGroup)
    {
        if (string.IsNullOrWhiteSpace(connectionType))
        {
            throw new ArgumentException("Connection type cannot be null or whitespace.", nameof(connectionType));
        }

        if (string.IsNullOrWhiteSpace(providerGroup))
        {
            throw new ArgumentException("Provider group cannot be null or whitespace.", nameof(providerGroup));
        }

        lock (_lock)
        {
            _connectionTypes.Add(connectionType);

            if (!_groupedConnectionTypes.TryGetValue(providerGroup, out var types))
            {
                types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _groupedConnectionTypes[providerGroup] = types;
            }

            types.Add(connectionType);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetRegisteredConnectionTypes()
    {
        lock (_lock)
        {
            return [.. _connectionTypes];
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetConnectionTypesForGroup(string providerGroup)
    {
        if (string.IsNullOrWhiteSpace(providerGroup))
        {
            throw new ArgumentException("Provider group cannot be null or whitespace.", nameof(providerGroup));
        }

        lock (_lock)
        {
            return _groupedConnectionTypes.TryGetValue(providerGroup, out var types)
                ? [.. types]
                : [];
        }
    }
}
