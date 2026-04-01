namespace Minnaloushe.Core.ClientProviders.Abstractions.Factories;

public interface IClientProviderFactory<out TProvider, in TOptions>
{
    /// <summary>
    /// Determines whether this factory can create a provider with the given options.
    /// </summary>
    /// <param name="options">The repository options containing connection configuration.</param>
    /// <returns>True if this factory can handle creation with these options; otherwise false.</returns>
    bool CanCreate(TOptions options);

    /// <summary>
    /// Creates a client provider for the specified connection.
    /// </summary>
    /// <param name="connectionName">The name of the connection.</param>
    /// <returns>The client provider instance.</returns>
    TProvider Create(string connectionName);
}