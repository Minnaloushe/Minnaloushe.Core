using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minnaloushe.Core.Toolbox.JsonConfiguration;

public class JsonSerializerSettings
{
    internal static readonly string SectionName = "JsonSerializer";

    public JsonSerializerDefaults Defaults { get; init; } = JsonSerializerDefaults.General;
    public bool WriteIndented { get; set; } = false;
    public bool PropertyNameCaseInsensitive { get; set; } = true;
    public bool AllowTrailingCommas { get; set; } = true;
    public bool IgnoreReadOnlyProperties { get; set; } = false;
    public bool IncludeFields { get; set; } = false;

    public int MaxDepth { get; set; } = 64;

    public JsonIgnoreCondition DefaultIgnoreCondition { get; set; } = JsonIgnoreCondition.Never;

    // Reference handling – very common to configure
    public ReferenceHandlingMode ReferenceHandling { get; set; } = ReferenceHandlingMode.IgnoreCycles;

    // Naming policy – string → mapped in code
    public string? PropertyNamingPolicy { get; set; }   // "CamelCase", "SnakeCaseLower", etc.

    // Number handling
    public JsonNumberHandling NumberHandling { get; set; } = JsonNumberHandling.Strict;

    // Dictionary key policy (rare, but useful)
    public string? DictionaryKeyPolicy { get; set; }

    // You can add more, but avoid things like Converters / TypeInfoResolver / Encoder
    // → those are almost always code-only
}