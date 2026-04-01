namespace Minnaloushe.Core.ApiClient;

public abstract class ApiClientSettings
{
    public string BaseAddress { get; init; } = string.Empty;
    public TimeSpan Timeout { get; init; }
}