using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LocalCliRunner.Api.Workspace;

namespace LocalCliRunner.Api.Infrastructure;

public record StageExecutionRequest(
    string Profile,
    string InputText,
    Dictionary<string, string>? AllOutputs = null,
    IReadOnlyList<string>? ImagePaths = null,
    string? WorkspacePath = null,
    string? WorkspaceKey = null);

public record StageExecutionResult(
    string WorkspacePath,
    string OutputFile,
    string Output,
    string? Warning,
    TokenUsage? Tokens,
    string? Stderr);

public class StageExecutionService(
    PromptBuilder promptBuilder,
    ICliRunner cliRunner,
    PiiTokenizer piiTokenizer,
    WorkspaceManager workspaceManager,
    SettingsService settingsService,
    RepoSearchService repoSearch,
    ILogger<StageExecutionService> logger)
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    public bool IsValidProfile(string profile) => StageProfiles.ValidProfiles.Contains(profile);

    public string GetOutputFile(string profile) => StageProfiles.GetOutputFile(profile);

    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionRequest request,
        Func<string, Task>? onChunk = null,
        CancellationToken ct = default)
    {
        if (!IsValidProfile(request.Profile))
            throw new InvalidOperationException($"Unknown profile: {request.Profile}");

        var workspacePath = request.WorkspacePath
            ?? workspaceManager.Create(request.WorkspaceKey ?? $"s-{Guid.NewGuid().ToString("N")[..6]}");

        EnsureWorkspaceDirectories(workspacePath);

        var layout = new WorkspaceLayout(workspacePath);
        var outputFile = GetOutputFile(request.Profile);

        await File.WriteAllTextAsync(layout.InputFile, request.InputText, ct);
        await WriteAllOutputsAsync(layout, request.AllOutputs, ct);

        var (tokenizedInput, piiMap) = piiTokenizer.Tokenize(request.InputText);
        var model = cliRunner is ClaudeVertexRunner
            ? null
            : settingsService.GetModelForStage(request.Profile);

        RunnerExecutionResult runnerResult;

        if (request.Profile == "patch")
        {
            runnerResult = await ExecutePatchAsync(
                request.Profile,
                tokenizedInput,
                piiMap,
                workspacePath,
                request.ImagePaths,
                model,
                onChunk,
                ct);
        }
        else
        {
            var promptInput = await BuildPromptInputAsync(request.Profile, tokenizedInput, request.AllOutputs, ct);
            runnerResult = await ExecuteSingleAsync(
                request.Profile,
                promptInput,
                piiMap,
                workspacePath,
                request.ImagePaths,
                model,
                onChunk,
                ct);
        }

        await File.WriteAllTextAsync(layout.OutputFile(outputFile), runnerResult.Output, ct);
        if (!string.IsNullOrWhiteSpace(runnerResult.Stderr))
            await File.WriteAllTextAsync(layout.LogFile, runnerResult.Stderr, ct);

        var warning = await RunVerifyScriptAsync(request.Profile, runnerResult.Output);

        return new StageExecutionResult(
            workspacePath,
            outputFile,
            runnerResult.Output,
            warning,
            runnerResult.Tokens,
            runnerResult.Stderr);
    }

    private async Task WriteAllOutputsAsync(WorkspaceLayout layout, Dictionary<string, string>? allOutputs, CancellationToken ct)
    {
        if (allOutputs is null)
            return;

        foreach (var (stage, content) in allOutputs)
        {
            if (string.IsNullOrWhiteSpace(content))
                continue;

            if (StageProfiles.OutputFiles.TryGetValue(stage, out var outFileName))
                await File.WriteAllTextAsync(layout.OutputFile(outFileName), content, ct);
        }
    }

    private static void EnsureWorkspaceDirectories(string workspacePath)
    {
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(workspacePath, "out"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "logs"));
    }

    private async Task<string> BuildPromptInputAsync(
        string profile,
        string tokenizedInput,
        Dictionary<string, string>? allOutputs,
        CancellationToken ct)
    {
        var promptInput = tokenizedInput;

        if (profile is "code-analysis-be" or "code-analysis-fe")
        {
            var gh = settingsService.Get().GitHub;
            var keywords = RepoSearchService.ExtractSearchKeywords(tokenizedInput);
            var searchTasks = new List<Task<(string Label, List<RepoFile> Files)>>();

            if (profile == "code-analysis-be" && !string.IsNullOrWhiteSpace(gh.BackendRepoUrl))
                searchTasks.Add(SearchRepoAsync("Backend", gh.BackendRepoUrl, keywords, ct));

            if (profile == "code-analysis-fe" && !string.IsNullOrWhiteSpace(gh.FrontendRepoUrl))
                searchTasks.Add(SearchRepoAsync("Frontend", gh.FrontendRepoUrl, keywords, ct));

            if (searchTasks.Count > 0)
            {
                var results = await Task.WhenAll(searchTasks);
                var sections = results
                    .Where(r => r.Files.Count > 0)
                    .Select(r => $"## {r.Label} 코드 파일\n\n{RepoSearchService.BuildContext(r.Files)}");

                var combined = string.Join("\n", sections);
                if (!string.IsNullOrWhiteSpace(combined))
                    promptInput = $"{tokenizedInput}\n\n{combined}";
            }

            if (profile == "code-analysis-fe"
                && allOutputs?.TryGetValue("design", out var designJson) == true
                && !string.IsNullOrWhiteSpace(designJson))
            {
                promptInput = $"{promptInput}\n\n## Design Package (design.json)\n\n{designJson}";
            }
        }

        if (profile == "patch")
        {
            var gh = settingsService.Get().GitHub;
            var keywords = RepoSearchService.ExtractSearchKeywords(tokenizedInput);
            var searchTasks = new List<Task<(string Label, List<RepoFile> Files)>>();

            if (!string.IsNullOrWhiteSpace(gh.FrontendRepoUrl))
                searchTasks.Add(SearchRepoAsync("Frontend", gh.FrontendRepoUrl, keywords, ct));
            if (!string.IsNullOrWhiteSpace(gh.BackendRepoUrl))
                searchTasks.Add(SearchRepoAsync("Backend", gh.BackendRepoUrl, keywords, ct));

            if (searchTasks.Count > 0)
            {
                var results = await Task.WhenAll(searchTasks);
                var sections = results
                    .Where(r => r.Files.Count > 0)
                    .Select(r => $"## {r.Label} 코드 파일\n\n{RepoSearchService.BuildContext(r.Files)}");

                var combined = string.Join("\n", sections);
                if (!string.IsNullOrWhiteSpace(combined))
                    promptInput = $"{tokenizedInput}\n\n{combined}";
            }
        }

        return promptInput;
    }

    private async Task<RunnerExecutionResult> ExecuteSingleAsync(
        string profile,
        string promptInput,
        Dictionary<string, string> piiMap,
        string workspacePath,
        IReadOnlyList<string>? imagePaths,
        string? model,
        Func<string, Task>? onChunk,
        CancellationToken ct)
    {
        var prompt = await promptBuilder.BuildAsync(profile, promptInput);
        await File.WriteAllTextAsync(new WorkspaceLayout(workspacePath).PromptFile, prompt, ct);

        var runnerResult = await ExecutePromptAsync(prompt, workspacePath, model, imagePaths, onChunk, ct);
        var output = await TransformOutputAsync(profile, runnerResult.Stdout, piiMap, ct);

        return runnerResult with { Output = output };
    }

    private async Task<RunnerExecutionResult> ExecutePatchAsync(
        string profile,
        string tokenizedInput,
        Dictionary<string, string> piiMap,
        string workspacePath,
        IReadOnlyList<string>? imagePaths,
        string? model,
        Func<string, Task>? onChunk,
        CancellationToken ct)
    {
        var gh = settingsService.Get().GitHub;
        var keywords = RepoSearchService.ExtractSearchKeywords(tokenizedInput);

        var repoTargets = new List<(string Label, string Url)>();
        if (!string.IsNullOrWhiteSpace(gh.FrontendRepoUrl))
            repoTargets.Add(("Frontend", gh.FrontendRepoUrl));
        if (!string.IsNullOrWhiteSpace(gh.BackendRepoUrl))
            repoTargets.Add(("Backend", gh.BackendRepoUrl));

        if (repoTargets.Count >= 2)
        {
            var searchResults = await Task.WhenAll(repoTargets.Select(r => SearchRepoAsync(r.Label, r.Url, keywords, ct)));
            var reposWithFiles = searchResults.Where(r => r.Files.Count > 0).ToList();

            if (reposWithFiles.Count >= 2)
            {
                var allPatches = new List<JsonElement>();
                TokenUsage? totalUsage = null;
                var stderrParts = new List<string>();

                foreach (var (label, files) in reposWithFiles)
                {
                    if (onChunk is not null)
                        await onChunk($"\n\n// ===== {label} 패치 생성 중... =====\n\n");

                    var repoInput = $"{tokenizedInput}\n\n## {label} 코드 파일\n\n{RepoSearchService.BuildContext(files)}";
                    var prompt = await promptBuilder.BuildAsync(profile, repoInput);
                    await File.WriteAllTextAsync(new WorkspaceLayout(workspacePath).PromptFile, prompt, ct);

                    var repoRunner = await ExecutePromptAsync(prompt, workspacePath, model, imagePaths, onChunk, ct);
                    var repoOutput = await TransformOutputAsync(profile, repoRunner.Stdout, piiMap, ct);

                    if (!string.IsNullOrWhiteSpace(repoRunner.Stderr))
                        stderrParts.Add(repoRunner.Stderr);

                    if (repoRunner.Tokens is not null)
                    {
                        totalUsage = totalUsage is null
                            ? repoRunner.Tokens
                            : new TokenUsage(
                                totalUsage.InputTokens + repoRunner.Tokens.InputTokens,
                                totalUsage.OutputTokens + repoRunner.Tokens.OutputTokens);
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(repoOutput);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            allPatches.AddRange(doc.RootElement.EnumerateArray().Select(item => item.Clone()));
                    }
                    catch (JsonException ex)
                    {
                        logger.LogWarning(ex, "Failed to merge patch output for {Label}", label);
                    }
                }

                return new RunnerExecutionResult(
                    JsonSerializer.Serialize(allPatches, PrettyJson),
                    string.Join(Environment.NewLine, stderrParts.Where(s => !string.IsNullOrWhiteSpace(s))),
                    totalUsage,
                    "");
            }
        }

        var fallbackInput = await BuildPromptInputAsync(profile, tokenizedInput, null, ct);
        return await ExecuteSingleAsync(profile, fallbackInput, piiMap, workspacePath, imagePaths, model, onChunk, ct);
    }

    private async Task<RunnerExecutionResult> ExecutePromptAsync(
        string prompt,
        string workspacePath,
        string? model,
        IReadOnlyList<string>? imagePaths,
        Func<string, Task>? onChunk,
        CancellationToken ct)
    {
        if (onChunk is null)
        {
            var result = await cliRunner.RunAsync(prompt, workspacePath, model, imagePaths, ct);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"CLI exited with code {result.ExitCode}. {result.Stderr}");

            return new RunnerExecutionResult(result.Stdout, result.Stderr, result.Usage, result.Stdout);
        }

        var buffer = new StringBuilder();
        var usage = await cliRunner.StreamAsync(prompt, workspacePath, async chunk =>
        {
            buffer.Append(chunk);
            await onChunk(chunk);
        }, model, imagePaths, ct);

        return new RunnerExecutionResult(buffer.ToString(), null, usage, buffer.ToString());
    }

    private async Task<string> TransformOutputAsync(
        string profile,
        string stdout,
        Dictionary<string, string> piiMap,
        CancellationToken ct)
    {
        var restored = piiTokenizer.Detokenize(stdout.TrimEnd(), piiMap);

        if (StageProfiles.ExpectsJsonArray(profile))
        {
            restored = StripCodeFence(restored);
            restored = ExtractJsonContent(restored, expectArray: true);
        }
        else if (StageProfiles.ExpectsJsonObject(profile))
        {
            restored = StripCodeFence(restored);
            restored = ExtractJsonContent(restored, expectArray: false);
        }

        var stylePath = promptBuilder.GetStyleInjectPath(profile);
        if (!string.IsNullOrEmpty(stylePath) && restored.Contains("<!--STYLE-->", StringComparison.Ordinal))
        {
            var css = await File.ReadAllTextAsync(stylePath, ct);
            restored = restored.Replace("<!--STYLE-->", $"<style>\n{css}\n</style>", StringComparison.Ordinal);
        }

        return restored;
    }

    private async Task<(string Label, List<RepoFile> Files)> SearchRepoAsync(
        string label,
        string repoUrl,
        string keywords,
        CancellationToken ct)
    {
        var budgetKb = settingsService.Get().CodeBudgetKb;
        var files = await repoSearch.SearchAsync(repoUrl, keywords, maxTotalChars: budgetKb * 1000, ct: ct);
        return (label, files);
    }

    private static string StripCodeFence(string text)
    {
        var s = text.TrimStart();
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline >= 0)
                s = s[(firstNewline + 1)..];
        }

        var trimmed = s.TrimEnd();
        if (trimmed.EndsWith("```", StringComparison.Ordinal))
            s = trimmed[..trimmed.LastIndexOf("```", StringComparison.Ordinal)];

        return s.Trim();
    }

    private static string ExtractJsonContent(string text, bool expectArray)
    {
        var open = expectArray ? '[' : '{';
        var close = expectArray ? ']' : '}';

        var start = text.IndexOf(open);
        if (start < 0)
            return text;

        var end = text.LastIndexOf(close);
        if (end < start)
            return text;

        return text[start..(end + 1)];
    }

    private async Task<string?> RunVerifyScriptAsync(string profile, string outputContent)
    {
        var builtInWarning = RunBuiltInVerify(profile, outputContent);
        if (profile == "jira" || builtInWarning is not null)
            return builtInWarning;

        var scriptPath = promptBuilder.GetVerifyScriptPath(profile);
        if (string.IsNullOrEmpty(scriptPath))
            return null;

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, outputContent);

            var scriptContent = (await File.ReadAllTextAsync(scriptPath))
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            var psi = new ProcessStartInfo("bash", "-s")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.Environment["CONTENT_FILE"] = tempFile.Replace('\\', '/');

            using var proc = Process.Start(psi)!;
            await proc.StandardInput.WriteAsync(scriptContent);
            proc.StandardInput.Close();

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var message = (stdout + stderr).Trim().TrimEnd('\n');
                return string.IsNullOrWhiteSpace(message) ? "검증 실패" : message;
            }
        }
        catch
        {
            // Ignore environments without bash.
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* ignore */ }
        }

        return null;
    }

    private static string? RunBuiltInVerify(string profile, string outputContent) =>
        profile switch
        {
            "jira" => VerifyJiraOutput(outputContent),
            "patch" => VerifyPatchOutput(outputContent),
            "design" => VerifyDesignOutput(outputContent),
            _ => null,
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
                if (!item.TryGetProperty("path", out _))
                    missing.Add("path");
                if (!item.TryGetProperty("content", out _))
                    missing.Add("content");
                if (missing.Count > 0)
                    break;
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

    private static string? VerifyDesignOutput(string outputContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(outputContent);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return "Design 결과는 JSON object여야 합니다.";

            var root = doc.RootElement;
            var missing = new List<string>();

            foreach (var field in new[] { "version", "meta", "layout", "sections", "components", "handoff" })
            {
                if (!root.TryGetProperty(field, out _))
                    missing.Add($"\"{field}\"");
            }

            return missing.Count > 0 ? $"누락 필드: {string.Join(" ", missing)}" : null;
        }
        catch (JsonException ex)
        {
            return $"Design JSON 파싱 실패: {ex.Message}";
        }
    }

    private record RunnerExecutionResult(string Output, string? Stderr, TokenUsage? Tokens, string Stdout);
}
