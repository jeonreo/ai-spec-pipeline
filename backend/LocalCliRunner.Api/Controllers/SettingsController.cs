using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(SettingsService settingsService) : ControllerBase
{
    // GET /api/settings
    [HttpGet]
    public IActionResult Get() => Ok(settingsService.Get());

    // PUT /api/settings
    [HttpPut]
    public IActionResult Update([FromBody] PipelineSettings settings)
    {
        settingsService.Save(settings);
        return Ok(settingsService.Get());
    }
}
