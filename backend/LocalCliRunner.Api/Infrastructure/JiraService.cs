using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalCliRunner.Api.Infrastructure;

public class JiraConfig
{
    public string BaseUrl    { get; set; } = "";
    public string Email      { get; set; } = "";
    public string ApiToken   { get; set; } = "";
}

public class JiraProject
{
    public string Key  { get; set; } = "";
    public string Name { get; set; } = "";
}

public class JiraIssueType
{
    public string Id   { get; set; } = "";
    public string Name { get; set; } = "";
}

public record CreateIssueRequest(
    string ProjectKey,
    string IssueTypeId,
    string Summary,
    Dictionary<string, string> Description,
    List<string> AcceptanceCriteria,
    string? SpecContent = null);

public class JiraService
{
    private readonly HttpClient  _http;
    private readonly JiraConfig  _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public JiraService(IConfiguration configuration)
    {
        _config = configuration.GetSection("Jira").Get<JiraConfig>() ?? new();

        // 환경변수로 ApiToken 오버라이드 (JIRA__APITOKEN 또는 JIRA_API_TOKEN)
        var token = Environment.GetEnvironmentVariable("JIRA__APITOKEN")
                 ?? Environment.GetEnvironmentVariable("JIRA_API_TOKEN");
        if (!string.IsNullOrEmpty(token))
            _config.ApiToken = token;

        _http = new HttpClient();

        if (IsConfigured)
        {
            var cred = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.Email}:{_config.ApiToken}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", cred);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public string IssueUrl(string key) => $"{_config.BaseUrl.TrimEnd('/')}/browse/{key}";

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_config.BaseUrl) &&
        !string.IsNullOrEmpty(_config.Email) &&
        !string.IsNullOrEmpty(_config.ApiToken);

    public async Task<List<JiraProject>> GetProjectsAsync()
    {
        var url = $"{_config.BaseUrl.TrimEnd('/')}/rest/api/3/project/search?maxResults=100&orderBy=name";
        var res = await _http.GetAsync(url);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("values")
            .EnumerateArray()
            .Select(p => new JiraProject
            {
                Key  = p.GetProperty("key").GetString()  ?? "",
                Name = p.GetProperty("name").GetString() ?? "",
            })
            .ToList();
    }

    public async Task<List<JiraIssueType>> GetIssueTypesAsync(string projectKey)
    {
        var url = $"{_config.BaseUrl.TrimEnd('/')}/rest/api/3/project/{projectKey}";
        var res = await _http.GetAsync(url);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("issueTypes")
            .EnumerateArray()
            .Select(t => new JiraIssueType
            {
                Id   = t.GetProperty("id").GetString()   ?? "",
                Name = t.GetProperty("name").GetString() ?? "",
            })
            .ToList();
    }

    public async Task<string> CreateIssueAsync(CreateIssueRequest req)
    {
        var adf = BuildAdfDescription(req.Description, req.AcceptanceCriteria);

        var body = new
        {
            fields = new
            {
                project     = new { key = req.ProjectKey },
                issuetype   = new { id  = req.IssueTypeId },
                summary     = req.Summary,
                description = adf,
            }
        };

        var json    = JsonSerializer.Serialize(body, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var res     = await _http.PostAsync($"{_config.BaseUrl.TrimEnd('/')}/rest/api/3/issue", content);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var issueKey = doc.RootElement.GetProperty("key").GetString()!;

        if (!string.IsNullOrEmpty(req.SpecContent))
            await AttachFileAsync(issueKey, "spec.md", req.SpecContent);

        return issueKey;
    }

    private async Task AttachFileAsync(string issueKey, string filename, string text)
    {
        var url = $"{_config.BaseUrl.TrimEnd('/')}/rest/api/3/issue/{issueKey}/attachments";

        using var form = new MultipartFormDataContent();
        var bytes       = Encoding.UTF8.GetBytes(text);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        form.Add(fileContent, "file", filename);

        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { { "X-Atlassian-Token", "no-check" } },
            Content = form,
        };
        (await _http.SendAsync(req)).EnsureSuccessStatusCode();
    }

    /// <summary>jira.json description 오브젝트 + AC → Atlassian Document Format(ADF)</summary>
    private static object BuildAdfDescription(Dictionary<string, string> desc, List<string> ac)
    {
        var nodes = new List<object>();

        foreach (var (label, text) in desc)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            nodes.Add(new
            {
                type    = "paragraph",
                content = new object[]
                {
                    new { type = "text", text = $"{label}: ", marks = new[] { new { type = "strong" } } },
                    new { type = "text", text },
                },
            });
        }

        if (ac.Count > 0)
        {
            nodes.Add(new
            {
                type    = "paragraph",
                content = new[] { new { type = "text", text = "완료 조건 (Acceptance Criteria)", marks = new[] { new { type = "strong" } } } },
            });
            nodes.Add(new
            {
                type    = "bulletList",
                content = ac
                    .Select(item => (object)new
                    {
                        type    = "listItem",
                        content = new object[]
                        {
                            new
                            {
                                type    = "paragraph",
                                content = new[] { new { type = "text", text = item } },
                            },
                        },
                    })
                    .ToArray(),
            });
        }

        return new { version = 1, type = "doc", content = nodes.ToArray() };
    }
}
