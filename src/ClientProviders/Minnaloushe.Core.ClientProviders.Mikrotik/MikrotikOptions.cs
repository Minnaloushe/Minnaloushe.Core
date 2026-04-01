using Minnaloushe.Core.Toolbox.DictionaryExtensions;
using Minnaloushe.Core.Toolbox.StringExtensions;
using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.ClientProviders.Mikrotik;

public record MikrotikOptions : VaultStoredOptions
{
    public const string SectionName = "Router";
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 0;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool ConnectOnStartup { get; init; } = true;
    public override bool IsEmpty => Host.IsNullOrWhiteSpace()
                                    || Username.IsNullOrWhiteSpace()
                                    || Password.IsNullOrWhiteSpace()
                                    || Port == 0;
    public override VaultStoredOptions ApplyVaultData(IDictionary<string, object> vaultData)
    {
        return this with
        {
            Host = vaultData.GetStringValue(nameof(Host)) ?? Host,
            Port = vaultData.GetIntValue(nameof(Port)) ?? Port,
            Username = vaultData.GetStringValue(nameof(Username)) ?? Username,
            Password = vaultData.GetStringValue(nameof(Password)) ?? Password
        };
    }
}