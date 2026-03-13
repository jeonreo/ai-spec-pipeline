using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(SettingsService settingsService, ICliRunner cliRunner) : ControllerBase
{
    // GET /api/settings
    [HttpGet]
    public IActionResult Get()
    {
        var settings = settingsService.Get();
        var isVertex = cliRunner is ClaudeVertexRunner;
        return Ok(new { stageModels = settings.StageModels, github = settings.GitHub, isVertex });
    }

    // PUT /api/settings
    [HttpPut]
    public IActionResult Update([FromBody] PipelineSettings settings)
    {
        settingsService.Save(settings);
        return Ok(settingsService.Get());
    }
}
