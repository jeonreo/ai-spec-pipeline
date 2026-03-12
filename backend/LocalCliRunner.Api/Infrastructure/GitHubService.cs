using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LocalCliRunner.Api.Infrastructure;

public record GitHubRepoInfo(string Owner, string Repo, string DefaultBranch, string HtmlUrl);
public record GitHubFileResult(string Path, string Content, string Sha);
public record GitHubSearchItem(string Path, string HtmlUrl);

/// <summary>
/// GitHub REST API v3 래퍼.
/// 인증: Personal Access Token (appsettings.json GitHub:Token 또는 환경변수 GitHub__Token)
/// </summary>
public class GitHubService(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    ILogger<GitHubService> logger)
{
    private string Token => config["GitHub:Token"] ?? "";

    // "https://github.com/owner/repo" or "https://github.com/owner/repo.git" → (owner, repo)
    public static (string owner, string repo) ParseRepoUrl(string repoUrl)
    {
        var cleaned = repoUrl.Trim().TrimEnd('/');
        // .git 접미사 제거 (TrimEnd(char[])는 개별 문자를 제거하므로 사용 금지)
        if (cleaned.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^4];

        var uri   = new Uri(cleaned);
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length < 2)
            throw new ArgumentException($"올바른 GitHub 저장소 URL이 아닙니다: {repoUrl}");
        return (parts[0], parts[1]);
    }

    public async Task<GitHubRepoInfo> GetRepoInfoAsync(string repoUrl, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);
        var json = await GetAsync($"repos/{owner}/{repo}", ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new GitHubRepoInfo(
            owner, repo,
            root.GetProperty("default_branch").GetString() ?? "main",
            root.GetProperty("html_url").GetString() ?? repoUrl);
    }

    public async Task<List<GitHubSearchItem>> SearchCodeAsync(string repoUrl, string query, int maxResults = 10, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);
        // GitHub Code Search API: keyword + repo scope
        var q = Uri.EscapeDataString($"{query} repo:{owner}/{repo}");
        var json = await GetAsync($"search/code?q={q}&per_page={maxResults}", ct);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => new GitHubSearchItem(
                item.GetProperty("path").GetString() ?? "",
                item.GetProperty("html_url").GetString() ?? ""))
            .ToList();
    }

    public async Task<GitHubFileResult> GetFileContentAsync(string repoUrl, string path, string? refName = null, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);
        var url = $"repos/{owner}/{repo}/contents/{path.TrimStart('/')}";
        if (!string.IsNullOrEmpty(refName)) url += $"?ref={Uri.EscapeDataString(refName)}";

        var json = await GetAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var base64 = root.GetProperty("content").GetString() ?? "";
        var content = Encoding.UTF8.GetString(Convert.FromBase64String(base64.Replace("\n", "")));
        var sha     = root.GetProperty("sha").GetString() ?? "";
        return new GitHubFileResult(path, content, sha);
    }

    public async Task<string> CreateBranchAsync(string repoUrl, string branchName, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);
        var info = await GetRepoInfoAsync(repoUrl, ct);

        // Get SHA of default branch tip
        var refJson = await GetAsync($"repos/{owner}/{repo}/git/ref/heads/{Uri.EscapeDataString(info.DefaultBranch)}", ct);
        using var refDoc = JsonDocument.Parse(refJson);
        var sha = refDoc.RootElement.GetProperty("object").GetProperty("sha").GetString()!;

        var body = JsonSerializer.Serialize(new { @ref = $"refs/heads/{branchName}", sha });
        await PostAsync($"repos/{owner}/{repo}/git/refs", body, ct);
        logger.LogInformation("Created branch {Branch} at {Sha}", branchName, sha[..8]);
        return sha;
    }

    public async Task UpsertFileAsync(string repoUrl, string branchName, string path, string content, string? existingSha, string commitMessage, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));

        object payload = existingSha is not null
            ? new { message = commitMessage, content = base64Content, branch = branchName, sha = existingSha }
            : new { message = commitMessage, content = base64Content, branch = branchName };

        await PutAsync($"repos/{owner}/{repo}/contents/{path.TrimStart('/')}", JsonSerializer.Serialize(payload), ct);
        logger.LogInformation("Upserted {Path} on branch {Branch}", path, branchName);
    }

    public async Task<string> CreatePullRequestAsync(string repoUrl, string branchName, string title, string body, bool draft = true, CancellationToken ct = default)
    {
        var (owner, repo) = ParseRepoUrl(repoUrl);
        var info = await GetRepoInfoAsync(repoUrl, ct);

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            head  = branchName,
            @base = info.DefaultBranch,
            draft,
        });

        var response = await PostAsync($"repos/{owner}/{repo}/pulls", payload, ct);
        using var doc = JsonDocument.Parse(response);
        var prUrl = doc.RootElement.GetProperty("html_url").GetString() ?? "";
        logger.LogInformation("Created PR: {Url}", prUrl);
        return prUrl;
    }

    // ── HTTP helpers ──────────────────────────────────────────────

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        using var client   = CreateClient();
        using var response = await client.GetAsync(path, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<string> PostAsync(string path, string jsonBody, CancellationToken ct)
    {
        using var client  = CreateClient();
        using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<string> PutAsync(string path, string jsonBody, CancellationToken ct)
    {
        using var client  = CreateClient();
        using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var response = await client.PutAsync(path, content, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private HttpClient CreateClient()
    {
        var client = httpFactory.CreateClient("github");
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ai-spec-pipeline/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return client;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"GitHub API 오류 {(int)response.StatusCode}: {body}");
        }
    }
}
