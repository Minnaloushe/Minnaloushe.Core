using Minnaloushe.Core.ApiClient.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Minnaloushe.Core.ApiClient;

public abstract class ApiClientBase
{
    private readonly IHttpClientAdapter _httpClient;
    private readonly JsonSerializerSettings _jsonSettings;

    protected ApiClientBase(
        IHttpClientAdapter httpClient,
        JsonSerializerSettings jsonSettings
    )
    {
        _httpClient = httpClient;
        _jsonSettings = jsonSettings;
    }

    protected ApiClientBase(
        IHttpClientAdapter httpClient
    )
    {
        _httpClient = httpClient;
        _jsonSettings = GetDefaultJsonSerializerSettings();
    }

    protected ApiRequest CreateRequest(HttpMethod method, string path)
    {
        return new ApiRequest(_httpClient, method, path, _jsonSettings);
    }

    protected JsonSerializerSettings GetDefaultJsonSerializerSettings()
    {
        var result = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DefaultValueHandling = DefaultValueHandling.Include,
            NullValueHandling = NullValueHandling.Ignore
        };

        result.Converters.Add(new StringEnumConverter());

        return result;
    }
}