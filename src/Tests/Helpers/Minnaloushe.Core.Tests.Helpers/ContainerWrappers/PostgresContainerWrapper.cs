using Minnaloushe.Core.Toolbox.TestHelpers;
using Testcontainers.PostgreSql;

namespace Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

public class PostgresContainerWrapper : ContainerWrapperBase<PostgreSqlBuilder, PostgreSqlContainer, PostgreSqlConfiguration>
{
    protected override PostgreSqlBuilder CreateBuilder() => new(Image.FromDefaultRegistry(ImageName));

    protected override PostgreSqlBuilder InitContainer(PostgreSqlBuilder builder)
    {
        return builder
            .WithUsername(Username)
            .WithPassword(Password)
            .WithDatabase(AppDb);
    }
    protected override string ImageName => "postgres:18-alpine";
    protected override ushort ContainerPort => 5432;
#pragma warning disable CA1822
    public string AppDb => "appDb";
#pragma warning restore CA1822
}