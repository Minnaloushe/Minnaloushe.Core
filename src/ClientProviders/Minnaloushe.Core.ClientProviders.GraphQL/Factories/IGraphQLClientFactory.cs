using GraphQL.Client.Http;
using Minnaloushe.Core.ClientProviders.GraphQL.Options;

namespace Minnaloushe.Core.ClientProviders.GraphQL.Factories;

public interface IGraphQLClientFactory
{
    GraphQLHttpClient Create(GraphQLOptions options);
}