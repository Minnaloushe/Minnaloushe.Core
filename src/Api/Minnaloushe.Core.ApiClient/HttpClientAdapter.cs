using Minnaloushe.Core.Api.Contracts.Exceptions;
using Minnaloushe.Core.ApiClient.Interfaces;

namespace Minnaloushe.Core.ApiClient;

public class HttpClientAdapter : IHttpClientAdapter
{
    private readonly HttpClient _httpClient;

    public HttpClientAdapter(
        HttpClient httpClient
    )
    {
        if (httpClient.BaseAddress == null)
        {
            throw new ArgumentNullException(nameof(httpClient), $"{nameof(httpClient.BaseAddress)} cannot be null");
        }

        _httpClient = httpClient;
    }

    public Uri BaseAddress => _httpClient.BaseAddress!;

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new ApiSendRequestException(request, "Send request failed", ex);
        }
    }
}