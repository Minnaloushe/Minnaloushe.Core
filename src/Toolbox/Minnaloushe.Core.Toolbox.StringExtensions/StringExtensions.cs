namespace Minnaloushe.Core.Toolbox.StringExtensions;

public static class StringExtensions
{
    extension(string? s)
    {
        public string Mask(int start = 8, int end = 8)
        {
            return s is null ? string.Empty : (s.Length <= start + end + 8) ? new string('*', s.Length) : $"{s[..start]}***{s[^end..]}";
        }

        public bool NotNullOrEmpty() => !string.IsNullOrEmpty(s);
        public bool IsNotNullOrWhiteSpace() => !string.IsNullOrWhiteSpace(s);
        public bool IsNullOrWhiteSpace() => string.IsNullOrWhiteSpace(s);
    }
}

public static class TypeExtensions
{
    public static string GetFriendlyName(this Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var genericName = type.Name[..type.Name.IndexOf('`')];

        var genericArgs = type.GetGenericArguments()
            .Select(GetFriendlyName);

        return $"{genericName}<{string.Join(", ", genericArgs)}>";
    }
}