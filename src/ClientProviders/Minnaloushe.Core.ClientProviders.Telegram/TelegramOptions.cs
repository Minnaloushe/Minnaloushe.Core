using Minnaloushe.Core.Toolbox.DictionaryExtensions;
using Minnaloushe.Core.Toolbox.StringExtensions;
using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.ClientProviders.Telegram;

public record TelegramOptions : VaultStoredOptions
{
    internal static string SectionName => "Telegram";

    public override bool IsEmpty => BotToken.IsNullOrWhiteSpace();

    public override VaultStoredOptions ApplyVaultData(IDictionary<string, object> vaultData)
    {
        return this with
        {
            BotToken = vaultData.GetStringValue(nameof(BotToken)) ?? BotToken,
            ChatId = vaultData.GetLongValue(nameof(ChatId)) ?? ChatId
        };
    }

    public string Key { get; init; } = string.Empty;
    public string BotToken { get; init; } = string.Empty;
    public long ChatId { get; init; }
}