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
    SettingsService settingsService,
    RepoSearchService repoSearch) : ControllerBase
{
    private static readonly HashSet<string> ValidProfiles =
        ["intake", "spec", "jira", "qa", "design", "code-analysis", "patch"];

    private static readonly Dictionary<string, string> OutputFiles = new()
    {
        ["intake"]        = "intake.md",
        ["spec"]          = "spec.md",
        ["jira"]          = "jira.json",
        ["qa"]            = "qa.md",
        ["design"]        = "design.json",
        ["code-analysis"] = "code-analysis.md",
        ["patch"]         = "patch.json",
    };

    // POST /api/run/{profile}
    [HttpPost("{profile}")]
    public IActionResult Run(string profile, [FromBody] RunRequest request)
    {
        if (!ValidProfiles.Contains(profile))
            return BadRequest(new { error = $"Unknown profile: {profile}" });

        var stageModel = cliRunner is ClaudeVertexRunner
            ? null
            : settingsService.GetModelForStage(profile);
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
    public async Task<IActionResult> GetJob(string jobId)
    {
        var job = registry.Get(jobId);
        if (job is null) return NotFound();

        // OutputContent는 메모리 절약을 위해 보관하지 않음 — 완료된 경우 파일에서 읽는다.
        string? outputContent = null;
        if (job.Status == Domain.JobStatus.Done && job.OutputFile is not null)
        {
            var layout  = new WorkspaceLayout(job.WorkspacePath);
            var outPath = layout.OutputFile(job.OutputFile);
            if (System.IO.File.Exists(outPath))
                outputContent = await System.IO.File.ReadAllTextAsync(outPath);
        }

        return Ok(new
        {
            jobId         = job.Id,
            status        = job.Status.ToString().ToLower(),
            workspacePath = job.WorkspacePath,
            outputFile    = job.OutputFile,
            outputContent,
            preview       = outputContent?[..Math.Min(2000, outputContent.Length)],
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

        // code-analysis / patch: FE·BE 저장소를 병렬 검색해 컨텍스트로 주입
        var promptInput = tokenizedInput;
        if (profile is "code-analysis" or "patch")
        {
            var gh       = settingsService.Get().GitHub;
            var keywords = RepoSearchService.ExtractSearchKeywords(request.InputText);

            // 설정된 저장소를 모두 병렬 검색
            var searchTasks = new List<Task<(string Label, List<RepoFile> Files)>>();
            if (!string.IsNullOrWhiteSpace(gh.FrontendRepoUrl))
                searchTasks.Add(SearchRepoAsync("Frontend", gh.FrontendRepoUrl, keywords, ct));
            if (!string.IsNullOrWhiteSpace(gh.BackendRepoUrl))
                searchTasks.Add(SearchRepoAsync("Backend",  gh.BackendRepoUrl,  keywords, ct));

            if (searchTasks.Count > 0)
            {
                var results = await Task.WhenAll(searchTasks);
                var sections = results
                    .Where(r => r.Files.Count > 0)
                    .Select(r => $"## {r.Label} 코드 파일\n\n{RepoSearchService.BuildContext(r.Files)}");
                var combined = string.Join("\n", sections);
                if (!string.IsNullOrEmpty(combined))
                    promptInput = $"{tokenizedInput}\n\n{combined}";
            }
        }

        var prompt = await promptBuilder.BuildAsync(profile, promptInput);

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

        // Vertex AI는 claude-sonnet-4-6 고정 (null → ClaudeVertexRunner.DefaultModel 사용)
        var model = cliRunner is ClaudeVertexRunner
            ? null
            : settingsService.GetModelForStage(profile);

        TokenUsage? usage;
        try
        {
            usage = await cliRunner.StreamAsync(prompt, workspacePath, async chunk =>
            {
                fullOutput.Append(chunk);
                var json = JsonSerializer.Serialize(new { chunk });
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }, model, null, ct);
        }
        catch (Exception ex)
        {
            var errJson = JsonSerializer.Serialize(new { error = ex.Message });
            await Response.WriteAsync($"data: {errJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
            return;
        }

        var restored = piiTokenizer.Detokenize(fullOutput.ToString().TrimEnd(), piiMap);

        // JSON 출력 스테이지: 마크다운 코드블록 마커 제거
        if (profile is "jira" or "design" or "patch")
            restored = StripCodeFence(restored);

        // <!--STYLE--> 마커를 실제 CSS로 교체 (design 전용)
        var stylePath = promptBuilder.GetStyleInjectPath(profile);
        if (!string.IsNullOrEmpty(stylePath) && restored.Contains("<!--STYLE-->", StringComparison.Ordinal))
        {
            var css = await System.IO.File.ReadAllTextAsync(stylePath, ct);
            restored = restored.Replace("<!--STYLE-->", $"<style>\n{css}\n</style>", StringComparison.Ordinal);
        }

        var outFile = OutputFiles.GetValueOrDefault(profile, $"{profile}.md");
        await System.IO.File.WriteAllTextAsync(layout.OutputFile(outFile), restored, ct);

        var warning = await RunVerifyScriptAsync(profile, restored);

        object? tokens = usage is null ? null : new { inputTokens = usage.InputTokens, outputTokens = usage.OutputTokens };
        var doneJson = JsonSerializer.Serialize(new { done = true, output = restored, warning, tokens });
        await Response.WriteAsync($"data: {doneJson}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    // POST /api/run/stream-files/{profile} — SSE 스트리밍 (이미지/파일 첨부 지원)
    [HttpPost("stream-files/{profile}")]
    [RequestSizeLimit(50_000_000)]
    public async Task StreamRunWithFiles(string profile, [FromForm] string inputText, [FromForm] string? allOutputsJson, CancellationToken ct)
    {
        if (!ValidProfiles.Contains(profile))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var (tokenizedInput, piiMap) = piiTokenizer.Tokenize(inputText);
        var prompt = await promptBuilder.BuildAsync(profile, tokenizedInput);

        var workspacePath = workspaceManager.Create($"s-{Guid.NewGuid().ToString("N")[..6]}");
        var layout = new WorkspaceLayout(workspacePath);
        await System.IO.File.WriteAllTextAsync(layout.InputFile, inputText, ct);
        await System.IO.File.WriteAllTextAsync(layout.PromptFile, prompt, ct);

        if (!string.IsNullOrEmpty(allOutputsJson))
        {
            var allOutputs = JsonSerializer.Deserialize<Dictionary<string, string>>(allOutputsJson);
            if (allOutputs is not null)
            {
                foreach (var (stage, content) in allOutputs)
                {
                    if (OutputFiles.TryGetValue(stage, out var outFileName) && !string.IsNullOrEmpty(content))
                        await System.IO.File.WriteAllTextAsync(layout.OutputFile(outFileName), content, ct);
                }
            }
        }

        // 업로드된 이미지 파일을 임시 폴더에 저장
        var tempDir    = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..8]);
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

            var model      = cliRunner is ClaudeVertexRunner ? null : settingsService.GetModelForStage(profile);
            var fullOutput = new StringBuilder();
            TokenUsage? usage;
            try
            {
                usage = await cliRunner.StreamAsync(prompt, workspacePath, async chunk =>
                {
                    fullOutput.Append(chunk);
                    var json = JsonSerializer.Serialize(new { chunk });
                    await Response.WriteAsync($"data: {json}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }, model, imagePaths.Count > 0 ? imagePaths : null, ct);
            }
            catch (Exception ex)
            {
                var errJson = JsonSerializer.Serialize(new { error = ex.Message });
                await Response.WriteAsync($"data: {errJson}\n\n", ct);
                await Response.Body.FlushAsync(ct);
                return;
            }

            var restored = piiTokenizer.Detokenize(fullOutput.ToString().TrimEnd(), piiMap);
            if (profile is "jira" or "design" or "patch")
                restored = StripCodeFence(restored);

            var stylePath = promptBuilder.GetStyleInjectPath(profile);
            if (!string.IsNullOrEmpty(stylePath) && restored.Contains("<!--STYLE-->", StringComparison.Ordinal))
            {
                var css = await System.IO.File.ReadAllTextAsync(stylePath, ct);
                restored = restored.Replace("<!--STYLE-->", $"<style>\n{css}\n</style>", StringComparison.Ordinal);
            }

            var outFile = OutputFiles.GetValueOrDefault(profile, $"{profile}.md");
            await System.IO.File.WriteAllTextAsync(layout.OutputFile(outFile), restored, ct);

            var warning  = await RunVerifyScriptAsync(profile, restored);
            object? tokens = usage is null ? null : new { inputTokens = usage.InputTokens, outputTokens = usage.OutputTokens };
            var doneJson = JsonSerializer.Serialize(new { done = true, output = restored, warning, tokens });
            await Response.WriteAsync($"data: {doneJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    // GET /api/policy
    [HttpGet("/api/policy")]
    public async Task<IActionResult> GetPolicy()
    {
        var content = await promptBuilder.ReadPolicyAsync();
        return Ok(new { content });
    }

    private async Task<(string Label, List<RepoFile> Files)> SearchRepoAsync(string label, string repoUrl, string keywords, CancellationToken ct)
    {
        var files = await repoSearch.SearchAsync(repoUrl, keywords, ct: ct);
        return (label, files);
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
        if (profile == "design")
            return null;

        var builtInWarning = RunBuiltInVerify(profile, outputContent);
        if (profile == "jira" || builtInWarning is not null)
            return builtInWarning;

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

    private static string? RunBuiltInVerify(string profile, string outputContent) =>
        profile switch
        {
            "jira"  => VerifyJiraOutput(outputContent),
            "patch" => VerifyPatchOutput(outputContent),
            _       => null,
        };

    private static string? VerifyPatchOutput(string outputContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(outputContent);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return "Patch 결과는 JSON array여야 합니다.";

            var missing = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("path", out _))    missing.Add("path");
                if (!item.TryGetProperty("content", out _)) missing.Add("content");
                if (missing.Count > 0) break;
            }
            return missing.Count > 0 ? $"누락 필드: {string.Join(", ", missing.Distinct())}" : null;
        }
        catch (JsonException ex)
        {
            return $"Patch JSON 파싱 실패: {ex.Message}";
        }
    }

    private static string? VerifyJiraOutput(string outputContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(outputContent);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return "Jira 결과는 JSON object여야 합니다.";

            var root = doc.RootElement;
            var missing = new List<string>();

            if (!root.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.String)
                missing.Add("\"summary\"");

            if (!root.TryGetProperty("description", out var description) || description.ValueKind != JsonValueKind.Object)
                missing.Add("\"description\"");

            var hasAcceptanceCriteria =
                root.TryGetProperty("acceptance_criteria", out var acceptanceCriteria) && acceptanceCriteria.ValueKind == JsonValueKind.Array
                || root.TryGetProperty("acceptanceCriteria", out acceptanceCriteria) && acceptanceCriteria.ValueKind == JsonValueKind.Array;

            if (!hasAcceptanceCriteria)
                missing.Add("\"acceptance_criteria\"");

            return missing.Count > 0 ? $"누락 필드: {string.Join(" ", missing)}" : null;
        }
        catch (JsonException ex)
        {
            return $"Jira JSON 파싱 실패: {ex.Message}";
        }
    }
}

[ApiController]
[Route("api/history")]
public class HistoryController(WorkspaceManager workspaceManager) : ControllerBase
{
    private static readonly string[] StageFiles =
        ["intake.md", "spec.md", "jira.json", "qa.md", "design.json", "design.html"];

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
        var dir = workspaceManager.GetById(id);
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

    // DELETE /api/history/{id}
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        return workspaceManager.Delete(id) ? NoContent() : NotFound();
    }
}

public record RunRequest(
    string InputText,
    Dictionary<string, string>? AllOutputs = null
);
