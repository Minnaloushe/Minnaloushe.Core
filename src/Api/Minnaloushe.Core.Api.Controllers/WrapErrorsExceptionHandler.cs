using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Api.Contracts;
using Minnaloushe.Core.Api.Contracts.Exceptions;
using Newtonsoft.Json;

namespace Minnaloushe.Core.Api.Controllers;

internal class WrapErrorsExceptionHandler(ILogger<WrapErrorsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Api error");

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/json";

        var code = (exception as ApiException)?.StatusCode ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(ApiResponse.Error(exception, code)),
            cancellationToken);

        return true;
    }
}