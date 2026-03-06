using System.Diagnostics;
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
    WorkspaceManager workspaceManager,
    SettingsService settingsService) : ControllerBase
{
    private static readonly HashSet<string> ValidProfiles =
        ["intake", "spec", "jira", "qa", "design"];

    private static readonly Dictionary<string, string> OutputFiles = new()
    {
        ["intake"] = "intake.md", ["spec"] = "spec.md",
        ["jira"]   = "jira.json", ["qa"]   = "qa.md", ["design"] = "design.html",
    };

    // POST /api/run/{profile}
    [HttpPost("{profile}")]
    public IActionResult Run(string profile, [FromBody] RunRequest request)
    {
        if (!ValidProfiles.Contains(profile))
            return BadRequest(new { error = $"Unknown profile: {profile}" });

        var stageModel = settingsService.GetModelForStage(profile);
        var command = new RunStageCommand(request.InputText, profile, stageModel);
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
        await System.IO.File.WriteAllTextAsync(layout.InputFile, request.InputText, ct);
        await System.IO.File.WriteAllTextAsync(layout.PromptFile, prompt, ct);

        // 현재 세션의 다른 스테이지 출력물도 함께 저장 → 히스토리에서 전체 세션 복원 가능
        if (request.AllOutputs is not null)
        {
            foreach (var (stage, content) in request.AllOutputs)
            {
                if (OutputFiles.TryGetValue(stage, out var outFileName) && !string.IsNullOrEmpty(content))
                    await System.IO.File.WriteAllTextAsync(layout.OutputFile(outFileName), content, ct);
            }
        }

        var fullOutput = new StringBuilder();

        var model = settingsService.GetModelForStage(profile);

        await cliRunner.StreamAsync(prompt, workspacePath, async chunk =>
        {
            fullOutput.Append(chunk);
            var json = JsonSerializer.Serialize(new { chunk });
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }, model, ct);

        var restored = piiTokenizer.Detokenize(fullOutput.ToString().TrimEnd(), piiMap);

        // jira: 마크다운 코드블록 마커 제거 (```json ... ```)
        if (profile == "jira")
            restored = StripCodeFence(restored);

        // <!--STYLE--> 마커를 실제 CSS로 교체 (design 전용)
        var stylePath = promptBuilder.GetStyleInjectPath(profile);
        if (!string.IsNullOrEmpty(stylePath))
        {
            var css = await System.IO.File.ReadAllTextAsync(stylePath, ct);
            restored = restored.Replace("<!--STYLE-->", $"<style>\n{css}\n</style>", StringComparison.Ordinal);
        }

        var outFile = OutputFiles.GetValueOrDefault(profile, $"{profile}.md");
        await System.IO.File.WriteAllTextAsync(layout.OutputFile(outFile), restored, ct);

        var warning = await RunVerifyScriptAsync(profile, restored);

        var doneJson = JsonSerializer.Serialize(new { done = true, output = restored, warning });
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

    private static string StripCodeFence(string text)
    {
        var s = text.TrimStart();
        if (s.StartsWith("```"))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline >= 0) s = s[(firstNewline + 1)..];
        }
        if (s.TrimEnd().EndsWith("```"))
            s = s[..s.TrimEnd().LastIndexOf("```")];
        return s.Trim();
    }

    private async Task<string?> RunVerifyScriptAsync(string profile, string outputContent)
    {
        var scriptPath = promptBuilder.GetVerifyScriptPath(profile);
        if (string.IsNullOrEmpty(scriptPath)) return null;

        var tempFile = Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(tempFile, outputContent);

            var scriptContent = (await System.IO.File.ReadAllTextAsync(scriptPath))
                .Replace("\r\n", "\n").Replace("\r", "\n");

            var psi = new ProcessStartInfo("bash", "-s")
            {
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };
            // 내용은 temp 파일로 전달 (env var는 긴 JSON/Windows에서 불안정)
            psi.Environment["CONTENT_FILE"] = tempFile.Replace('\\', '/');

            using var proc = Process.Start(psi)!;
            await proc.StandardInput.WriteAsync(scriptContent);
            proc.StandardInput.Close();

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
                return (stdout + stderr).Trim().TrimEnd('\n') is { Length: > 0 } msg ? msg : "검증 실패";
        }
        catch
        {
            // bash 없는 환경 등 — 무시
        }
        finally
        {
            try { System.IO.File.Delete(tempFile); } catch { /* ignore */ }
        }

        return null;
    }
}

[ApiController]
[Route("api/history")]
public class HistoryController(WorkspaceManager workspaceManager) : ControllerBase
{
    private static readonly string[] StageFiles =
        ["intake.md", "spec.md", "jira.json", "qa.md", "design.html"];

    // GET /api/history?page=1&pageSize=20&date=2026-03-05
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
                var name      = Path.GetFileName(dir);
                var inputFile = Path.Combine(dir, "input.txt");
                var inputText = System.IO.File.Exists(inputFile)
                    ? System.IO.File.ReadAllText(inputFile)
                    : "";
                var outDir    = Path.Combine(dir, "out");
                var stages    = System.IO.Directory.Exists(outDir)
                    ? System.IO.Directory.GetFiles(outDir).Select(Path.GetFileName).ToArray()
                    : [];

                return new
                {
                    id           = name,
                    inputPreview = inputText.Length > 120 ? inputText[..120] + "…" : inputText,
                    stages,
                };
            });

        return Ok(new { total, page, pageSize, items });
    }

    // GET /api/history/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var dir = workspaceManager.ListAll().FirstOrDefault(d => Path.GetFileName(d) == id);
        if (dir is null) return NotFound();

        var inputFile = Path.Combine(dir, "input.txt");
        var inputText = System.IO.File.Exists(inputFile)
            ? await System.IO.File.ReadAllTextAsync(inputFile)
            : "";

        var outDir  = Path.Combine(dir, "out");
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
}

public record RunRequest(string InputText, Dictionary<string, string>? AllOutputs = null);
