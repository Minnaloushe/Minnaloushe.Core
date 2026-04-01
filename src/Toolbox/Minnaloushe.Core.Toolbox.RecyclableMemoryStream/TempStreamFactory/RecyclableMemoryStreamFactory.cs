using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.ManagerWrapper;

namespace Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;

internal class RecyclableMemoryStreamFactory(IRecyclableMemoryStreamManagerWrapper managerWrapper) : ITempStreamFactory
{
    public Stream Create() => managerWrapper.GetStream();
}