using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minnaloushe.Core.Toolbox.JsonConfiguration;

internal static class JsonSerializerOptionsFactory
{
    public static JsonSerializerOptions Create(
        JsonSerializerSettings settings,
        Action<JsonSerializerOptions>? configureRuntime = null)
    {
        var options = new JsonSerializerOptions(settings.Defaults)
        {
            WriteIndented = settings.WriteIndented,
            PropertyNameCaseInsensitive = settings.PropertyNameCaseInsensitive,
            AllowTrailingCommas = settings.AllowTrailingCommas,
            IgnoreReadOnlyProperties = settings.IgnoreReadOnlyProperties,
            IncludeFields = settings.IncludeFields,
            MaxDepth = settings.MaxDepth,
            DefaultIgnoreCondition = settings.DefaultIgnoreCondition,
            ReferenceHandler = MapReferenceHandling(settings.ReferenceHandling),
            NumberHandling = settings.NumberHandling,
            // Naming policy mapping (the most requested configurable part)
            PropertyNamingPolicy = settings.PropertyNamingPolicy?.ToLowerInvariant() switch
            {
                "camelcase" => JsonNamingPolicy.CamelCase,
                "snakecaselower" => JsonNamingPolicy.SnakeCaseLower,
                "snakecaseupper" => JsonNamingPolicy.SnakeCaseUpper,
                "kebabcaselower" => JsonNamingPolicy.KebabCaseLower,
                "kebabcaseupper" => JsonNamingPolicy.KebabCaseUpper,
                _ => null
            },
            DictionaryKeyPolicy = settings?.DictionaryKeyPolicy?.ToLowerInvariant() switch
            {
                "camelcase" => JsonNamingPolicy.CamelCase,
                "snakecaselower" => JsonNamingPolicy.SnakeCaseLower,
                "snakecaseupper" => JsonNamingPolicy.SnakeCaseUpper,
                "kebabcaselower" => JsonNamingPolicy.KebabCaseLower,
                "kebabcaseupper" => JsonNamingPolicy.KebabCaseUpper,
                _ => null
            }
        };

        // Let caller add runtime-only things (converters, custom encoder, type resolver, etc.)
        configureRuntime?.Invoke(options);

        return options;
    }

    private static ReferenceHandler? MapReferenceHandling(ReferenceHandlingMode mode)
        => mode switch
        {
            ReferenceHandlingMode.IgnoreCycles => ReferenceHandler.IgnoreCycles,
            ReferenceHandlingMode.Preserve => ReferenceHandler.Preserve,
            _ => null   // default behavior
        };
}