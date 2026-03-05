using System.Text;
using System.Text.Json;
using LocalCliRunner.Api.Application;
using LocalCliRunner.Api.Domain;
using LocalCliRunner.Api.Infrastructure;
using LocalCliRunner.Api.Workspace;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/run")]
public class RunController(
    RunStageHandler handler,
    JobRegistry registry,
    PromptBuilder promptBuilder,
    ICliRunner cliRunner,
    PiiTokenizer piiTokenizer,
    WorkspaceManager workspaceManager) : ControllerBase
{
    private static readonly HashSet<string> ValidProfiles =
        ["intake", "spec", "jira", "qa", "design"];

    private static readonly Dictionary<string, string> OutputFiles = new()
    {
        ["intake"] = "intake.md", ["spec"] = "spec.md",
        ["jira"]   = "jira.json", ["qa"]   = "qa.md", ["design"] = "design.html",
    };

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

    // POST /api/run/stream/{profile}  — SSE 스트리밍
    [HttpPost("stream/{profile}")]
    public async Task StreamRun(string profile, [FromBody] RunRequest request, CancellationToken ct)
    {
        if (!ValidProfiles.Contains(profile))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var (tokenizedInput, piiMap) = piiTokenizer.Tokenize(request.InputText);
        var prompt = await promptBuilder.BuildAsync(profile, tokenizedInput);

        var workspacePath = workspaceManager.Create($"s-{Guid.NewGuid().ToString("N")[..6]}");
        var layout = new WorkspaceLayout(workspacePath);
        await File.WriteAllTextAsync(layout.InputFile, request.InputText, ct);
        await File.WriteAllTextAsync(layout.PromptFile, prompt, ct);

        var fullOutput = new StringBuilder();

        await cliRunner.StreamAsync(prompt, workspacePath, async chunk =>
        {
            fullOutput.Append(chunk);
            var json = JsonSerializer.Serialize(new { chunk });
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }, ct);

        var restored = piiTokenizer.Detokenize(fullOutput.ToString().TrimEnd(), piiMap);

        var outFile = OutputFiles.GetValueOrDefault(profile, $"{profile}.md");
        await File.WriteAllTextAsync(layout.OutputFile(outFile), restored, ct);

        var doneJson = JsonSerializer.Serialize(new { done = true, output = restored });
        await Response.WriteAsync($"data: {doneJson}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    // GET /api/policy
    [HttpGet("/api/policy")]
    public async Task<IActionResult> GetPolicy()
    {
        var content = await promptBuilder.ReadPolicyAsync();
        return Ok(new { content });
    }
}

public record RunRequest(string InputText);
