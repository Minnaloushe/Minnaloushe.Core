using Microsoft.AspNetCore.Mvc;
using Minnaloushe.Core.ClientProviders.Postgres;

namespace Template.WebApi.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PostgresRepositoryController : ControllerBase
{

    public async Task<IActionResult> TestResolution(
        [FromKeyedServices("TemplatePostgresRepository")]
        IPostgresClientProvider clientProvider,
        CancellationToken cancellationToken)
    {
        return Ok();
    }
}
