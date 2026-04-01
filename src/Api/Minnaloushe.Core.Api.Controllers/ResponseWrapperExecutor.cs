using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Api.Contracts;

namespace Minnaloushe.Core.Api.Controllers;

internal class ResponseWrapperExecutor(
    OutputFormatterSelector formatterSelector,
    IHttpResponseStreamWriterFactory writerFactory,
    ILoggerFactory loggerFactory,
    IOptions<MvcOptions> mvcOptions)
    : ObjectResultExecutor(formatterSelector, writerFactory, loggerFactory, mvcOptions)
{
    public override Task ExecuteAsync(ActionContext context, ObjectResult result)
    {
        var response = new ApiResponse { Data = result.Value };

        result.Value = response;

        return base.ExecuteAsync(context, result);
    }
}