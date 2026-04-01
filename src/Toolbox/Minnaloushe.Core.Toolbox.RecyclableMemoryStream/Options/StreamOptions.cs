namespace Minnaloushe.Core.Toolbox.RecyclableMemoryStream.Options;

public class StreamOptions
{
    internal static readonly string SectionName = "StreamOptions";

    public TempStreamType TempStreamType { get; init; } = TempStreamType.RecyclableMemoryStream;
    public RecyclableMemoryStreamOptions RecyclableMemoryStream { get; init; } = new();
}