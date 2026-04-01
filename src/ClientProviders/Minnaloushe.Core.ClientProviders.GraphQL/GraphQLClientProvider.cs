using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.GraphQL.Factories;
using Minnaloushe.Core.ClientProviders.GraphQL.Options;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;

namespace Minnaloushe.Core.ClientProviders.GraphQL;

internal class GraphQLClientProvider(
    IResolvedOptions<GraphQLOptions> options,
    IGraphQLClientFactory factory,
    ILogger<GraphQLClientProvider> logger)
    : IGraphQLClientProvider

{
    private GraphQLHttpClient _client = null!;

    public async Task<TResponse> GetResponseAsync<TResponse>(string query, object variables, CancellationToken ct)
    {
        var request = new GraphQLRequest(query, variables);

        var response = await _client.SendQueryAsync<TResponse>(request, ct);

        return response.Data;
    }

    public GraphQLHttpClient Client => _client;
    public Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        if (options.IsEmpty)
        {
            logger.LogWarning("GraphQL options configuration was not completed");
            return Task.FromResult(false);
        }

        if (options.Value.IsEmpty)
        {
            logger.LogWarning("GraphQL options are not properly configured.");
            return Task.FromResult(false);
        }

        _client = factory.Create(options.Value);

        return Task.FromResult(true);
    }
}