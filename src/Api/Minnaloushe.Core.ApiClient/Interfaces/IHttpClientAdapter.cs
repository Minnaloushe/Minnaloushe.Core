namespace Minnaloushe.Core.ApiClient.Interfaces;

public interface IHttpClientAdapter
{
    Uri BaseAddress { get; }
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ctx);
}