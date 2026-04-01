using Newtonsoft.Json;

namespace Minnaloushe.Core.ApiClient;

public class ApiInternalResponse
{
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly HttpResponseMessage _response;

    public ApiInternalResponse(HttpResponseMessage response, JsonSerializerSettings jsonSettings)
    {
        _response = response;
        _jsonSettings = jsonSettings;
    }

    public async Task<T?> ReadBodyAsync<T>(CancellationToken ct)
    {
        return JsonConvert.DeserializeObject<T>(await _response.Content.ReadAsStringAsync(ct).ConfigureAwait(false),
            _jsonSettings);
    }

    public async Task<Stream> ReadBodyAsStreamAsync(CancellationToken ct)
    {
        return await _response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }
}