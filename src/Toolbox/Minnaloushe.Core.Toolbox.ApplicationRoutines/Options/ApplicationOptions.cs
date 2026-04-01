namespace Minnaloushe.Core.Toolbox.ApplicationRoutines.Options;

public record ApplicationOptions
{
    public static readonly string SectionName = "Application";
    public string DataPath { get; init; } = "/data";
}