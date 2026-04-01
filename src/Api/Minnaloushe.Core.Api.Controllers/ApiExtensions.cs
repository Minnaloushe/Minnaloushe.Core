using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Minnaloushe.Core.Api.Controllers;

public static class ApiExtensions
{
    public static IServiceCollection AddWrappedResponseRoutines(this IServiceCollection services)
    {
        return services.AddExceptionHandler<WrapErrorsExceptionHandler>()
            .AddProblemDetails()
            .AddSingleton<IActionResultExecutor<ObjectResult>, ResponseWrapperExecutor>();
    }

    public static IApplicationBuilder UseWrappedControllers(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler();
    }
}