using System.Text.RegularExpressions;

namespace Minnaloushe.Core.MessageQueues.Routines;

//TODO: Refactor, inject as a dependency
public static partial class MqNaming
{
    [GeneratedRegex(@"[^a-zA-Z0-9._-]", RegexOptions.Compiled)]
    private static partial Regex InvalidChars { get; }

    public static string GetSafeName<T>()
    {
        return GetSafeName(typeof(T).FullName!);
    }

    public static string GetSafeName(Type type)
    {
        return GetSafeName(type.FullName!);
    }

    public static string GetSafeName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Type name cannot be null or empty.", nameof(fullName));
        }

        // Replace + (nested types) with .
        fullName = fullName.Replace('+', '.');

        // Replace invalid characters with _
        fullName = InvalidChars.Replace(fullName, "_");

        // Optional: lowercase for Kafka and general normalization
        return fullName.ToLowerInvariant();
    }
}
