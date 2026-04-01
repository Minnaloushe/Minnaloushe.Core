namespace Minnaloushe.Core.ClientProviders.Abstractions.Transactions;

internal class AmbientTransaction
{
    private static readonly AsyncLocal<TransactionScope?> AsyncLocalCurrent = new();

    internal static void SetScope(TransactionScope? scope)
    {
        AsyncLocalCurrent.Value = scope;
    }
    public static TransactionScope? Current => AsyncLocalCurrent.Value;
}

