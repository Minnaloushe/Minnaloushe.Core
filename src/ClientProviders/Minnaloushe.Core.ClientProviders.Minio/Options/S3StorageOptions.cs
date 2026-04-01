using Minnaloushe.Core.Toolbox.DictionaryExtensions;
using Minnaloushe.Core.Toolbox.StringExtensions;
using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.ClientProviders.Minio.Options;

public record S3StorageOptions : VaultStoredOptions
{
    internal static string SectionName => "S3";

    public string ServiceName { get; init; } = string.Empty;
    public string ServiceUrl { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public bool SyncRules { get; init; } = false;
    public LifecycleRule[] LifecycleRules { get; init; } = [];
    public string Key { get; init; } = string.Empty;
    public HttpClientOptions HttpClient { get; init; } = new();
    public override bool IsEmpty => (ServiceName.IsNullOrWhiteSpace() && ServiceUrl.IsNullOrWhiteSpace())
                                    || AccessKey.IsNullOrWhiteSpace()
                                    || SecretKey.IsNullOrWhiteSpace();

    public override VaultStoredOptions ApplyVaultData(IDictionary<string, object> vaultData)
    {
        return this with
        {
            ServiceUrl = vaultData.GetStringValue(nameof(ServiceUrl)) ?? ServiceUrl,
            AccessKey = vaultData.GetStringValue(nameof(AccessKey)) ?? AccessKey,
            SecretKey = vaultData.GetStringValue(nameof(SecretKey)) ?? SecretKey,
            ServiceName = vaultData.GetStringValue(nameof(ServiceName)) ?? ServiceName
        };
    }

    public record HttpClientOptions
    {
        public int MaxConnections { get; init; } = 100;
        public TimeSpan PolledConnectionLifetime { get; init; } = TimeSpan.FromMinutes(10);
        public bool EnableMultipleHttp2Connections { get; init; } = false;
    }
}