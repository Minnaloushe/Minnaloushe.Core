using Minnaloushe.Core.Api.Contracts;
using Minnaloushe.Core.ApiClient.Interfaces;
using Newtonsoft.Json;

namespace Minnaloushe.Core.ApiClient;

public abstract class WrappedApiClient(IHttpClientAdapter httpClient)
    : ApiClientBase(httpClient, new JsonSerializerSettings())
{
    protected async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string path,
        TRequest requestBody, CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Post, path)
            .AddBody(requestBody);

        var response = await request.SendAsync(ct);

        return await response.ReadBodyAsync<ApiResponse<TResponse>>(ct) ?? ApiResponse<TResponse>.Empty();
    }

    protected async Task<ApiResponse<TResponse>> GetAsync<TResponse>(string path,
        IEnumerable<(string Name, string Value)> parameters, CancellationToken ct)
    {
        var request = CreateRequest(HttpMethod.Get, path);

        foreach (var parameter in parameters)
        {
            request.AddQueryParameter(parameter.Name, parameter.Value);
        }

        var response = await request.SendAsync(ct);

        return await response.ReadBodyAsync<ApiResponse<TResponse>>(ct) ?? ApiResponse<TResponse>.Empty();
    }
}