using System.Text;
using System.Text.Json;
using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/github")]
public class GitHubController(
    GitHubService gitHub,
    SettingsService settingsService,
    ILogger<GitHubController> logger) : ControllerBase
{
    // GET /api/github/status
    // 설정된 FE/BE 저장소의 연결 상태를 확인한다.
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var gh     = settingsService.Get().GitHub;
        var repos  = new[] { ("frontend", gh.FrontendRepoUrl), ("backend", gh.BackendRepoUrl) };
        var result = new List<object>();

        foreach (var (label, url) in repos)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                result.Add(new { label, url = "", connected = false, error = "설정되지 않음" });
                continue;
            }
            try
            {
                var info = await gitHub.GetRepoInfoAsync(url, ct);
                result.Add(new { label, url, connected = true, repoName = $"{info.Owner}/{info.Repo}", defaultBranch = info.DefaultBranch });
            }
            catch (Exception ex)
            {
                result.Add(new { label, url, connected = false, error = ex.Message });
            }
        }

        return Ok(result);
    }

    // GET /api/github/file-content?repo=frontend|backend&path=...
    // Patch Diff 미리보기: 현재 저장소의 파일 원본 내용을 반환한다.
    [HttpGet("file-content")]
    public async Task<IActionResult> GetFileContent([FromQuery] string repo, [FromQuery] string path, CancellationToken ct)
    {
        var gh = settingsService.Get().GitHub;
        var repoUrl = repo?.ToLower() == "frontend" ? gh.FrontendRepoUrl : gh.BackendRepoUrl;
        if (string.IsNullOrWhiteSpace(repoUrl))
            return NotFound(new { error = "저장소 URL이 설정되지 않았습니다." });

        try
        {
            var file = await gitHub.GetFileContentAsync(repoUrl, path, ct: ct);
            return Ok(new { content = file.Content });
        }
        catch (Exception ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST /api/github/push
    // patch JSON의 repo 필드를 기준으로 FE·BE 저장소에 각각 브랜치를 생성하고 파일을 커밋한다.
    // PR은 생성하지 않는다.
    [HttpPost("push")]
    public async Task<IActionResult> PushBranch([FromBody] PushRequest request, CancellationToken ct)
    {
        if (request.Patches is null || request.Patches.Count == 0)
            return BadRequest(new { error = "patches가 비어있습니다." });

        var gh         = settingsService.Get().GitHub;
        var timestamp  = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var branchName = $"ai/draft-{timestamp}";

        var grouped = request.Patches
            .GroupBy(p => (p.Repo?.ToLower() == "frontend") ? "frontend" : "backend")
            .ToDictionary(g => g.Key, g => g.ToList());

        var repoMap = new Dictionary<string, string>
        {
            ["frontend"] = gh.FrontendRepoUrl,
            ["backend"]  = gh.BackendRepoUrl,
        };

        var pushed = new List<object>();

        foreach (var (repoLabel, patches) in grouped)
        {
            var repoUrl = repoMap.GetValueOrDefault(repoLabel);
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                logger.LogWarning("{Label} 저장소 URL이 설정되지 않아 건너뜀", repoLabel);
                continue;
            }

            try
            {
                logger.LogInformation("브랜치 푸시: branch={Branch}, repo={Repo} ({Label})", branchName, repoUrl, repoLabel);

                await gitHub.CreateBranchAsync(repoUrl, branchName, ct);

                foreach (var patch in patches)
                {
                    string? existingSha = null;
                    try { existingSha = (await gitHub.GetFileContentAsync(repoUrl, patch.Path, ct: ct)).Sha; }
                    catch { /* 새 파일 */ }

                    await gitHub.UpsertFileAsync(repoUrl, branchName, patch.Path, patch.Content, existingSha,
                        patch.Comment ?? $"AI: {patch.Path} 수정", ct);
                }

                var info       = await gitHub.GetRepoInfoAsync(repoUrl, ct);
                var branchUrl  = $"{info.HtmlUrl}/tree/{Uri.EscapeDataString(branchName)}";
                pushed.Add(new { label = repoLabel, branchName, branchUrl, filesCommitted = patches.Count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Label} 브랜치 푸시 실패", repoLabel);
                pushed.Add(new { label = repoLabel, branchName = (string?)null, error = ex.Message });
            }
        }

        if (pushed.Count == 0)
            return BadRequest(new { error = "생성 가능한 저장소가 없습니다. 저장소 URL을 확인하세요." });

        return Ok(new { results = pushed });
    }

    // POST /api/github/pr
    // 이미 푸시된 브랜치로 FE·BE 저장소에 Draft PR을 생성한다.
    [HttpPost("pr")]
    public async Task<IActionResult> CreatePr([FromBody] CreatePrRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.BranchName))
            return BadRequest(new { error = "branchName이 필요합니다." });

        var gh      = settingsService.Get().GitHub;
        var prTitle = request.Title ?? "AI Draft: 코드 변경 제안";

        var repoMap = new Dictionary<string, string>
        {
            ["frontend"] = gh.FrontendRepoUrl,
            ["backend"]  = gh.BackendRepoUrl,
        };

        // 요청된 repos만, 없으면 설정된 전체
        var targetRepos = (request.Repos is { Count: > 0 })
            ? request.Repos
            : repoMap.Keys.ToList();

        var created = new List<object>();

        foreach (var repoLabel in targetRepos)
        {
            var repoUrl = repoMap.GetValueOrDefault(repoLabel);
            if (string.IsNullOrWhiteSpace(repoUrl)) continue;

            try
            {
                logger.LogInformation("PR 생성: branch={Branch}, repo={Repo} ({Label})", request.BranchName, repoUrl, repoLabel);
                var patches = request.Patches?.Where(p =>
                    repoLabel == "frontend"
                        ? p.Repo?.ToLower() == "frontend"
                        : p.Repo?.ToLower() != "frontend"
                ).ToList() ?? [];

                var prBody = BuildPrBody(request.Title, request.SpecSummary, request.AnalysisSummary, patches, repoLabel);
                var prUrl  = await gitHub.CreatePullRequestAsync(repoUrl, request.BranchName, prTitle, prBody, draft: true, ct);
                created.Add(new { label = repoLabel, prUrl });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Label} PR 생성 실패", repoLabel);
                created.Add(new { label = repoLabel, prUrl = (string?)null, error = ex.Message });
            }
        }

        if (created.Count == 0)
            return BadRequest(new { error = "생성 가능한 저장소가 없습니다. 저장소 URL을 확인하세요." });

        return Ok(new { results = created });
    }

    private static string BuildPrBody(string? title, string? specSummary, string? analysisSummary, List<PatchFile> patches, string repoLabel)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## ⚠️ AI-Generated Draft ({repoLabel.ToUpper()}) — Review Required Before Merge");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(specSummary))
        {
            sb.AppendLine("## Spec 요약");
            sb.AppendLine(specSummary);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(analysisSummary))
        {
            sb.AppendLine("## 변경 분석");
            sb.AppendLine(analysisSummary);
            sb.AppendLine();
        }

        if (patches.Count > 0)
        {
            sb.AppendLine("## 변경 파일");
            foreach (var p in patches)
                sb.AppendLine($"- `{p.Path}`{(p.Comment is not null ? $" — {p.Comment}" : "")}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("🤖 Generated by AI Spec Pipeline");
        return sb.ToString();
    }
}

public record PatchFile(string Path, string Content, string? Repo = null, string? Comment = null);

public record PushRequest(
    string? Title,
    List<PatchFile>? Patches,
    string? SpecSummary     = null,
    string? AnalysisSummary = null);

public record CreatePrRequest(
    string BranchName,
    string? Title            = null,
    List<string>? Repos      = null,
    string? SpecSummary      = null,
    string? AnalysisSummary  = null,
    List<PatchFile>? Patches = null);
