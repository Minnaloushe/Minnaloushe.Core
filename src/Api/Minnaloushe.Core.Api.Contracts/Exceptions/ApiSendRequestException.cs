namespace Minnaloushe.Core.Api.Contracts.Exceptions;

public class ApiSendRequestException(HttpRequestMessage request, string message, Exception innerException)
    : ApiException(message: message, innerException: innerException)
{
    public HttpRequestMessage Request { get; } = request;
}