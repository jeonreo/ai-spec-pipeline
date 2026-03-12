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

    // POST /api/github/pr
    // patch JSON의 repo 필드를 기준으로 FE·BE 저장소에 각각 PR Draft를 생성한다.
    [HttpPost("pr")]
    public async Task<IActionResult> CreatePr([FromBody] CreatePrRequest request, CancellationToken ct)
    {
        if (request.Patches is null || request.Patches.Count == 0)
            return BadRequest(new { error = "patches가 비어있습니다." });

        var gh        = settingsService.Get().GitHub;
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var branchName = $"ai/draft-{timestamp}";
        var prTitle   = request.Title ?? "AI Draft: 코드 변경 제안";

        // repo 필드로 FE·BE 분리. repo 필드가 없으면 BE로 귀속.
        var grouped = request.Patches
            .GroupBy(p => (p.Repo?.ToLower() == "frontend") ? "frontend" : "backend")
            .ToDictionary(g => g.Key, g => g.ToList());

        var repoMap = new Dictionary<string, string>
        {
            ["frontend"] = gh.FrontendRepoUrl,
            ["backend"]  = gh.BackendRepoUrl,
        };

        var created = new List<object>();

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
                logger.LogInformation("PR 생성: branch={Branch}, repo={Repo} ({Label})", branchName, repoUrl, repoLabel);

                await gitHub.CreateBranchAsync(repoUrl, branchName, ct);

                foreach (var patch in patches)
                {
                    string? existingSha = null;
                    try { existingSha = (await gitHub.GetFileContentAsync(repoUrl, patch.Path, ct: ct)).Sha; }
                    catch { /* 새 파일 */ }

                    await gitHub.UpsertFileAsync(repoUrl, branchName, patch.Path, patch.Content, existingSha,
                        patch.Comment ?? $"AI: {patch.Path} 수정", ct);
                }

                var prBody = BuildPrBody(request, patches, repoLabel);
                var prUrl  = await gitHub.CreatePullRequestAsync(repoUrl, branchName, prTitle, prBody, draft: true, ct);
                created.Add(new { label = repoLabel, prUrl, branchName });
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

    private static string BuildPrBody(CreatePrRequest req, List<PatchFile> patches, string repoLabel)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## ⚠️ AI-Generated Draft ({repoLabel.ToUpper()}) — Review Required Before Merge");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(req.SpecSummary))
        {
            sb.AppendLine("## Spec 요약");
            sb.AppendLine(req.SpecSummary);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(req.AnalysisSummary))
        {
            sb.AppendLine("## 변경 분석");
            sb.AppendLine(req.AnalysisSummary);
            sb.AppendLine();
        }

        sb.AppendLine("## 변경 파일");
        foreach (var p in patches)
            sb.AppendLine($"- `{p.Path}`{(p.Comment is not null ? $" — {p.Comment}" : "")}");

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("🤖 Generated by AI Spec Pipeline");
        return sb.ToString();
    }
}

public record PatchFile(string Path, string Content, string? Repo = null, string? Comment = null);

public record CreatePrRequest(
    string? Title,
    List<PatchFile>? Patches,
    string? SpecSummary     = null,
    string? AnalysisSummary = null);
