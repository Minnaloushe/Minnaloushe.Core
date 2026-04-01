using Minnaloushe.Core.Toolbox.TestHelpers;
using Testcontainers.MongoDb;

namespace Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

public class MongoDbContainerWrapper : ContainerWrapperBase<MongoDbBuilder, MongoDbContainer, MongoDbConfiguration>
{
    protected override MongoDbBuilder CreateBuilder() => new(Image.FromDefaultRegistry(ImageName));

    protected override MongoDbBuilder InitContainer(MongoDbBuilder builder)
    {
        return builder
            .WithUsername(Username)
            .WithPassword(Password);
    }

    protected override string ImageName => "mongo:6.0";
    protected override ushort ContainerPort => 27017;

#pragma warning disable CA1822
    public string AuthDb => "admin";
    public string AppDb => "appDb";
#pragma warning restore CA1822
}