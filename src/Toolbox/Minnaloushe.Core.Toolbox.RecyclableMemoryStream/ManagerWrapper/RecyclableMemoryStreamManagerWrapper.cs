using Microsoft.Extensions.Options;
using Microsoft.IO;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.Options;

namespace Minnaloushe.Core.Toolbox.RecyclableMemoryStream.ManagerWrapper;

public class RecyclableMemoryStreamManagerWrapper(IOptions<StreamOptions> options)
    : IRecyclableMemoryStreamManagerWrapper
{
    private readonly RecyclableMemoryStreamManager _manager = new(new RecyclableMemoryStreamManager.Options()
    {
        BlockSize = options.Value.RecyclableMemoryStream.BlockSize,
        LargeBufferMultiple = options.Value.RecyclableMemoryStream.LargeBufferMultiple,
        MaximumBufferSize = options.Value.RecyclableMemoryStream.MaximumBufferSize,
        MaximumSmallPoolFreeBytes = options.Value.RecyclableMemoryStream.MaximumSmallPoolFreeBytes,
        MaximumLargePoolFreeBytes = options.Value.RecyclableMemoryStream.MaximumLargePoolFreeBytes,
        UseExponentialLargeBuffer = options.Value.RecyclableMemoryStream.UseExponentialLargeBuffer,
        MaximumStreamCapacity = options.Value.RecyclableMemoryStream.MaximumStreamCapacity,
        GenerateCallStacks = options.Value.RecyclableMemoryStream.GenerateCallStacks,
        AggressiveBufferReturn = options.Value.RecyclableMemoryStream.AggressiveBufferReturn,
        ThrowExceptionOnToArray = options.Value.RecyclableMemoryStream.ThrowExceptionOnToArray,
        ZeroOutBuffer = options.Value.RecyclableMemoryStream.ZeroOutBuffer
    });

    public RecyclableMemoryStreamManager GetManager() => _manager;

    public Microsoft.IO.RecyclableMemoryStream GetStream() => _manager.GetStream();

}