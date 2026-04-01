using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.Toolbox.TestHelpers;

public static class TestHostConfigurationExtensions
{
    public static IConfigurationBuilder AddConfiguration(this IConfigurationBuilder builder, object config)
    {
        var json = JsonSerializer.Serialize(config);
        return builder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
