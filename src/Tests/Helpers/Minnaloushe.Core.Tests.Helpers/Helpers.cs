namespace Minnaloushe.Core.Tests.Helpers;

public class Helpers
{
    public static string UniqueString(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N").ToLower()}";
    }
}