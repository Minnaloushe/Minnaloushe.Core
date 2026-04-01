using Microsoft.AspNetCore.Http;

namespace Minnaloushe.Core.Api.Contracts.Exceptions;

public class ApiException(
    int code = StatusCodes.Status500InternalServerError,
    string? message = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int StatusCode { get; init; } = code;
}