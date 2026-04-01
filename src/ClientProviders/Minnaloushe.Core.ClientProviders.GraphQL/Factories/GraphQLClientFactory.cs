using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Minnaloushe.Core.ClientProviders.GraphQL.Options;

namespace Minnaloushe.Core.ClientProviders.GraphQL.Factories;

public class GraphQLClientFactory : IGraphQLClientFactory
{
    public GraphQLHttpClient Create(GraphQLOptions options)
    {
        var httpClientOptions = new GraphQLHttpClientOptions
        {
            EndPoint = new Uri(options.Endpoint)
        };
        return new GraphQLHttpClient(httpClientOptions, new SystemTextJsonSerializer());
    }
}