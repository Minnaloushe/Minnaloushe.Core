using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;
using Minnaloushe.Core.ClientProviders.GraphQL.Factories;
using Minnaloushe.Core.ClientProviders.GraphQL.Options;

namespace Minnaloushe.Core.ClientProviders.GraphQL.Extensions;

public static class DependencyRegistration
{
    public static KeyedSingletonBuilder AddKeyedMinioClientProviders(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.RegisterKeyedClientProvider<
            GraphQLOptions,
            IGraphQLClientProvider,
            GraphQLClientProvider,
            IGraphQLClientFactory,
            GraphQLClientFactory>(
            configuration,
            sectionName: GraphQLOptions.SectionName,
            providerFactory: (sp, _, factory, resolvedOptions) =>
                new GraphQLClientProvider(
                    resolvedOptions,
                    factory,
                    sp.GetRequiredService<ILogger<GraphQLClientProvider>>()
                )
        );
    }
}