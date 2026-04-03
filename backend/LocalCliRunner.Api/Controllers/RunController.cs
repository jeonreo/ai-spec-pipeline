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
    StageExecutionService stageExecutionService,
    JobRegistry registry,
    PromptBuilder promptBuilder,
    ICliRunner cliRunner,
    SettingsService settingsService) : ControllerBase
{
    [HttpPost("{profile}")]
    public IActionResult Run(string profile, [FromBody] RunRequest request)
    {
        if (!stageExecutionService.IsValidProfile(profile))
            return BadRequest(new { error = $"Unknown profile: {profile}" });

        var stageModel = cliRunner is ClaudeVertexRunner
            ? null
            : settingsService.GetModelForStage(profile);
        var command = new RunStageCommand(request.InputText, profile, stageModel);
        var result = handler.Enqueue(command);

        return Accepted(new
        {
            jobId = result.JobId,
            status = result.Status.ToString().ToLower(),
            workspacePath = result.WorkspacePath,
        });
    }

    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetJob(string jobId)
    {
        var job = registry.Get(jobId);
        if (job is null)
            return NotFound();

        string? outputContent = null;
        if (job.Status == JobStatus.Done && job.OutputFile is not null)
        {
            var layout = new WorkspaceLayout(job.WorkspacePath);
            var outPath = layout.OutputFile(job.OutputFile);
            if (System.IO.File.Exists(outPath))
                outputContent = await System.IO.File.ReadAllTextAsync(outPath);
        }

        return Ok(new
        {
            jobId = job.Id,
            status = job.Status.ToString().ToLower(),
            workspacePath = job.WorkspacePath,
            outputFile = job.OutputFile,
            outputContent,
            preview = outputContent?[..Math.Min(2000, outputContent.Length)],
            error = job.Error,
        });
    }

    [HttpPost("stream/{profile}")]
    public async Task StreamRun(string profile, [FromBody] RunRequest request, CancellationToken ct)
    {
        if (!stageExecutionService.IsValidProfile(profile))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            var result = await stageExecutionService.ExecuteAsync(
                new StageExecutionRequest(profile, request.InputText, request.AllOutputs),
                async chunk =>
                {
                    var json = JsonSerializer.Serialize(new { chunk });
                    await Response.WriteAsync($"data: {json}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                },
                ct);

            object? tokens = result.Tokens is null
                ? null
                : new { inputTokens = result.Tokens.InputTokens, outputTokens = result.Tokens.OutputTokens };
            var doneJson = JsonSerializer.Serialize(new { done = true, output = result.Output, warning = result.Warning, tokens });
            await Response.WriteAsync($"data: {doneJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            var errJson = JsonSerializer.Serialize(new { error = ex.Message });
            await Response.WriteAsync($"data: {errJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpPost("stream-files/{profile}")]
    [RequestSizeLimit(50_000_000)]
    public async Task StreamRunWithFiles(string profile, [FromForm] string inputText, [FromForm] string? allOutputsJson, CancellationToken ct)
    {
        if (!stageExecutionService.IsValidProfile(profile))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..8]);
        var imagePaths = new List<string>();
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var file in Request.Form.Files)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif")
                {
                    var dest = Path.Combine(tempDir, Path.GetFileName(file.FileName));
                    await using var fs = System.IO.File.Create(dest);
                    await file.CopyToAsync(fs, ct);
                    imagePaths.Add(dest);
                }
            }

            var allOutputs = string.IsNullOrEmpty(allOutputsJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(allOutputsJson);

            var result = await stageExecutionService.ExecuteAsync(
                new StageExecutionRequest(profile, inputText, allOutputs, imagePaths.Count > 0 ? imagePaths : null),
                async chunk =>
                {
                    var json = JsonSerializer.Serialize(new { chunk });
                    await Response.WriteAsync($"data: {json}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                },
                ct);

            object? tokens = result.Tokens is null
                ? null
                : new { inputTokens = result.Tokens.InputTokens, outputTokens = result.Tokens.OutputTokens };
            var doneJson = JsonSerializer.Serialize(new { done = true, output = result.Output, warning = result.Warning, tokens });
            await Response.WriteAsync($"data: {doneJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            var errJson = JsonSerializer.Serialize(new { error = ex.Message });
            await Response.WriteAsync($"data: {errJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [HttpGet("/api/policy")]
    public async Task<IActionResult> GetPolicy()
    {
        var content = await promptBuilder.ReadPolicyAsync();
        return Ok(new { content });
    }
}

[ApiController]
[Route("api/history")]
public class HistoryController(WorkspaceManager workspaceManager) : ControllerBase
{
    private static readonly string[] StageFiles =
        ["intake.md", "spec.md", "jira.json", "qa.md", "design.json", "design.html", "code-analysis-be.md", "code-analysis-fe.md"];

    [HttpGet]
    public IActionResult List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? date = null)
    {
        var datePrefix = date?.Replace("-", "");

        var all = workspaceManager.ListAll()
            .Where(dir => datePrefix is null || Path.GetFileName(dir).StartsWith(datePrefix))
            .ToList();

        var total = all.Count;
        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(dir =>
            {
                var name = Path.GetFileName(dir);
                var inputFile = Path.Combine(dir, "input.txt");
                var inputText = System.IO.File.Exists(inputFile)
                    ? System.IO.File.ReadAllText(inputFile)
                    : "";
                var outDir = Path.Combine(dir, "out");
                var stages = System.IO.Directory.Exists(outDir)
                    ? System.IO.Directory.GetFiles(outDir).Select(Path.GetFileName).ToArray()
                    : [];

                return new
                {
                    id = name,
                    inputPreview = inputText.Length > 120 ? inputText[..120] + "…" : inputText,
                    stages,
                };
            });

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var dir = workspaceManager.GetById(id);
        if (dir is null)
            return NotFound();

        var inputFile = Path.Combine(dir, "input.txt");
        var inputText = System.IO.File.Exists(inputFile)
            ? await System.IO.File.ReadAllTextAsync(inputFile)
            : "";

        var outDir = Path.Combine(dir, "out");
        var outputs = new Dictionary<string, string>();

        if (System.IO.Directory.Exists(outDir))
        {
            foreach (var file in StageFiles)
            {
                var path = Path.Combine(outDir, file);
                if (System.IO.File.Exists(path))
                {
                    var stage = Path.GetFileNameWithoutExtension(file);
                    outputs[stage] = await System.IO.File.ReadAllTextAsync(path);
                }
            }
        }

        return Ok(new { id, inputText, outputs });
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id) =>
        workspaceManager.Delete(id) ? NoContent() : NotFound();
}

public record RunRequest(
    string InputText,
    Dictionary<string, string>? AllOutputs = null
);

[ApiController]
[Route("api/learn")]
public class LearnController(PromptBuilder promptBuilder) : ControllerBase
{
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] LearnApplyRequest request)
    {
        var applied = new List<string>();
        var errors = new List<string>();

        foreach (var patch in request.Patches)
        {
            try
            {
                var skillPath = promptBuilder.GetSkillPath(patch.Stage);
                if (!System.IO.File.Exists(skillPath))
                {
                    errors.Add($"{patch.Stage}: SKILL.md를 찾을 수 없음");
                    continue;
                }

                var existing = await System.IO.File.ReadAllTextAsync(skillPath);
                await System.IO.File.WriteAllTextAsync(skillPath + ".bak", existing);
                var updated = existing.TrimEnd() + "\n\n---\n\n## Learn Agent 추가 지침\n" + patch.SkillPatch.Trim();
                await System.IO.File.WriteAllTextAsync(skillPath, updated);
                applied.Add(patch.Stage);
            }
            catch (Exception ex)
            {
                errors.Add($"{patch.Stage}: {ex.Message}");
            }
        }

        return Ok(new { applied, errors });
    }
}

public record LearnPatch(string Stage, string SkillPatch);
public record LearnApplyRequest(List<LearnPatch> Patches);
