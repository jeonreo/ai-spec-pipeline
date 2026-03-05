using LocalCliRunner.Api.Application;
using LocalCliRunner.Api.Domain;
using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/run")]
public class RunController(RunStageHandler handler, JobRegistry registry) : ControllerBase
{
    private static readonly HashSet<string> ValidProfiles =
        ["intake", "spec", "jira", "qa", "design"];

    // POST /api/run/{profile}
    // Body: { "inputText": "..." }
    [HttpPost("{profile}")]
    public IActionResult Run(string profile, [FromBody] RunRequest request)
    {
        if (!ValidProfiles.Contains(profile))
            return BadRequest(new { error = $"Unknown profile: {profile}" });

        var command = new RunStageCommand(request.InputText, profile);
        var result  = handler.Enqueue(command);

        return Accepted(new
        {
            jobId         = result.JobId,
            status        = result.Status.ToString().ToLower(),
            workspacePath = result.WorkspacePath,
        });
    }

    // GET /api/run/{jobId}
    [HttpGet("{jobId}")]
    public IActionResult GetJob(string jobId)
    {
        var job = registry.Get(jobId);
        if (job is null) return NotFound();

        return Ok(new
        {
            jobId         = job.Id,
            status        = job.Status.ToString().ToLower(),
            workspacePath = job.WorkspacePath,
            outputFile    = job.OutputFile,
            outputContent = job.OutputContent,
            preview       = job.OutputContent?[..Math.Min(2000, job.OutputContent.Length)],
            error         = job.Error,
        });
    }
}

public record RunRequest(string InputText);
