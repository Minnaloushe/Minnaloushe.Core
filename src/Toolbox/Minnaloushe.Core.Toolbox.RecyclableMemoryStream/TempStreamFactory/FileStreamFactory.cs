namespace Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;

internal class FileStreamFactory : ITempStreamFactory
{
    public Stream Create() =>
        new FileStream(
            Path.GetTempFileName(),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.DeleteOnClose);

}