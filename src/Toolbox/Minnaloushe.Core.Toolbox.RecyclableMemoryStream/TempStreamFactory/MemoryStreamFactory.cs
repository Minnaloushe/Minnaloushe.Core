namespace Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;

internal class MemoryStreamFactory : ITempStreamFactory
{
    public Stream Create() => new MemoryStream();
}