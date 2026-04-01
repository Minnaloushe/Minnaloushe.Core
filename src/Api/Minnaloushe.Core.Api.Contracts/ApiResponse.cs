using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.ComponentModel;

namespace Minnaloushe.Core.Api.Contracts;

public record ApiResponse : ApiResponse<object>
{
    public static ApiResponse Success(object? data, int code = StatusCodes.Status200OK)
    {
        return new ApiResponse { Data = data, Code = code };
    }

    public static ApiResponse Error(Exception ex, int code = StatusCodes.Status500InternalServerError)
    {
        return new ApiResponse
        {
            Exception = ex,
            Code = code,
            Message = ex.Message
        };
    }

    public static ApiResponse Error(string message, int code = StatusCodes.Status500InternalServerError)
    {
        return new ApiResponse
        {
            Message = message,
            Code = code
        };
    }
}

public record ApiResponse<T>
{
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public T? Data { get; init; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Message { get; init; }

    [DefaultValue(StatusCodes.Status200OK)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public int Code { get; init; } = StatusCodes.Status200OK;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public Exception? Exception { get; init; }

    public static ApiResponse<T> Empty()
    {
        return new ApiResponse<T> { Code = StatusCodes.Status204NoContent };
    }

    public static ApiResponse<T> Success(T? data)
    {
        return new ApiResponse<T> { Data = data, Code = StatusCodes.Status200OK };
    }

    public static ApiResponse<T> NotFound()
    {
        return new ApiResponse<T>() { Code = StatusCodes.Status404NotFound };
    }
}