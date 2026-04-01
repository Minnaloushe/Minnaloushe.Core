using GraphQL.Client.Http;
using Minnaloushe.Core.ClientProviders.Abstractions.StaticClientProvider;
using Minnaloushe.Core.Toolbox.AsyncInitializer;

namespace Minnaloushe.Core.ClientProviders.GraphQL;

public interface IGraphQLClientProvider : IStaticClientProvider<GraphQLHttpClient>, IAsyncInitializer
{
    Task<TResponse> GetResponseAsync<TResponse>(string query, object variables, CancellationToken ct);
}