namespace Minnaloushe.Core.Toolbox.RecyclableMemoryStream.Options;

public record RecyclableMemoryStreamOptions
{
    public int BlockSize { get; init; } = 128 * 1024;
    public int LargeBufferMultiple { get; init; } = 1024 * 1024;
    public int MaximumBufferSize { get; init; } = 128 * 1024 * 1024;
    public long MaximumSmallPoolFreeBytes { get; init; } = 0;
    public long MaximumLargePoolFreeBytes { get; init; } = 0;
    public bool UseExponentialLargeBuffer { get; init; } = false;
    public long MaximumStreamCapacity { get; init; } = 0;
    public bool GenerateCallStacks { get; init; } = false;
    public bool AggressiveBufferReturn { get; set; } = false;
    public bool ThrowExceptionOnToArray { get; set; } = false;
    public bool ZeroOutBuffer { get; set; } = false;
}