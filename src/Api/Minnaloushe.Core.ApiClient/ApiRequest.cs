using Minnaloushe.Core.Api.Contracts.Exceptions;
using Minnaloushe.Core.ApiClient.Interfaces;
using Newtonsoft.Json;
using System.Net.Http.Json;

namespace Minnaloushe.Core.ApiClient;

public class ApiRequest
{
    private readonly IHttpClientAdapter _httpClient;
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly string _path;
    private readonly Dictionary<string, string> _queryParameters = [];
    private readonly HttpRequestMessage _requestMessage;

    public ApiRequest(IHttpClientAdapter httpClient,
        HttpMethod method,
        string path,
        JsonSerializerSettings jsonSettings)
    {
        _httpClient = httpClient;
        _path = path;
        _jsonSettings = jsonSettings;

        _requestMessage = new HttpRequestMessage
        {
            Method = method
        };
    }

    public ApiRequest AddQueryParameter(string name, string value)
    {
        _queryParameters[name] = value;
        return this;
    }

    public ApiRequest AddBody<T>(T body)
    {
        _requestMessage.Content = JsonContent.Create(body);

        return this;
    }

    public async Task<ApiInternalResponse> SendAsync(CancellationToken ct = default)
    {
        _requestMessage.RequestUri = GetUrl(_path, _queryParameters);

        try
        {
            return new ApiInternalResponse(await _httpClient.SendAsync(_requestMessage, ct), _jsonSettings);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiSendRequestException(_requestMessage, "Send request failed", ex);
        }
    }

    private Uri GetUrl(string path, Dictionary<string, string> queryParameters)
    {
        var queryString = string.Join("&", queryParameters.Select(v => $"{v.Key}={v.Value}"));

        return Uri.TryCreate(path, UriKind.Absolute, out var result) && !result.IsFile
            ? new UriBuilder(result)
            {
                Query = queryString
            }
                    .Uri
            : new UriBuilder(_httpClient.BaseAddress)
            {
                Path = path,
                Query = queryString
            }
                .Uri;
    }
}