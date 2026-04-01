using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.ApplicationRoutines.Options;

namespace Template.WebApi.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VolumesController(IOptions<ApplicationOptions> options) : ControllerBase
{
    [HttpPost("TestTempMount")]
    public async Task<IActionResult> TestTempMount()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "TestMount");
        Directory.CreateDirectory(tempPath);
        var testFilePath = Path.Combine(tempPath, "testfile.txt");
        await System.IO.File.WriteAllTextAsync(testFilePath, "This is a test file.");
        var fileContent = await System.IO.File.ReadAllTextAsync(testFilePath);
        // Clean up
        System.IO.File.Delete(testFilePath);
        Directory.Delete(tempPath);
        return Ok(new { Message = "Temporary mount test successful", FileContent = fileContent });
    }

    [HttpPost("TestDataMount")]
    public async Task<IActionResult> TestDataMount()
    {
        var dataPath = Path.Combine(options.Value.DataPath);
        Directory.CreateDirectory(dataPath);
        var testFilePath = Path.Combine(dataPath, "testfile.txt");
        await System.IO.File.WriteAllTextAsync(testFilePath, "This is a test file in data mount.");
        var fileContent = await System.IO.File.ReadAllTextAsync(testFilePath);
        // Clean up
        System.IO.File.Delete(testFilePath);

        return Ok(new { Message = "Data mount test successful", FileContent = fileContent });
    }
}