using Microsoft.AspNetCore.Mvc;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.Models;

namespace Template.WebApi.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MinioController : ControllerBase
{
    [HttpGet("ListBlobs")]
    public async Task<IActionResult> ListObjects(
        [FromKeyedServices("config-s3")]
        IS3StorageAdapter storageAdapter)
    {
        var result = new List<BlobInfo>();
        await foreach (var item in storageAdapter.ListBlobsAsync())
        {
            result.Add(item);
        }
        return Ok(result);
    }
}
