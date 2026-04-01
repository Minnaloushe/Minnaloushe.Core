using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.VaultService.Options;
using VaultSharp;

namespace Template.WebApi.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VaultController(IClientProvider<IVaultClient> clientProvider, IOptions<VaultOptions> options) : ControllerBase
{
    [HttpGet("GetValue")]
    public async Task<IActionResult> Get(string path, string key)
    {
        using var client = clientProvider.Acquire();

        var values = await client.Client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path, mountPoint: options.Value.MountPoint);

        var result = values.Data.Data.TryGetValue(key, out var value) ? value : null;

        return Ok(value);
    }

    [HttpGet("ListKeys")]
    public async Task<IActionResult> ListKeys(string path)
    {
        using var client = clientProvider.Acquire();
        var values = await client.Client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path, mountPoint: options.Value.MountPoint);
        return Ok(values.Data.Data.Keys);
    }
}
