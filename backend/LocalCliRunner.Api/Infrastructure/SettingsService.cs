using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalCliRunner.Api.Infrastructure;

public class GitHubSettings
{
    [JsonPropertyName("frontendRepoUrl")]
    public string FrontendRepoUrl { get; set; } = "";

    [JsonPropertyName("backendRepoUrl")]
    public string BackendRepoUrl { get; set; } = "";
}

public class PipelineSettings
{
    [JsonPropertyName("stageModels")]
    public Dictionary<string, string> StageModels { get; set; } = new()
    {
        ["intake"]        = "claude-haiku-4-5-20251001",
        ["spec"]          = "claude-sonnet-4-6",
        ["jira"]          = "claude-haiku-4-5-20251001",
        ["qa"]            = "claude-sonnet-4-6",
        ["design"]        = "claude-haiku-4-5-20251001",
        ["code-analysis"] = "claude-sonnet-4-6",
        ["patch"]         = "claude-sonnet-4-6",
    };

    [JsonPropertyName("github")]
    public GitHubSettings GitHub { get; set; } = new();
}

/// <summary>
/// pipeline-settings.json을 읽고 쓴다. 런타임에 재시작 없이 설정 변경 가능.
/// </summary>
public class SettingsService
{
    private readonly string _filePath;
    private PipelineSettings _current;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
    };

    public SettingsService(IConfiguration config, IWebHostEnvironment env)
    {
        // 설정 파일 경로: appsettings.json의 "SettingsFile" 키 → 없으면 wwwroot 옆 pipeline-settings.json
        var path = config["SettingsFile"]
            ?? Path.Combine(env.ContentRootPath, "pipeline-settings.json");
        _filePath = path;
        _current  = Load();
    }

    public PipelineSettings Get()
    {
        lock (_lock) return _current;
    }

    public void Save(PipelineSettings settings)
    {
        lock (_lock)
        {
            _current = settings;
            var json = JsonSerializer.Serialize(settings, _jsonOpts);
            File.WriteAllText(_filePath, json);
        }
    }

    public string GetModelForStage(string stage)
    {
        lock (_lock)
        {
            if (_current.StageModels.TryGetValue(stage, out var model) && !string.IsNullOrWhiteSpace(model))
                return model;
            return "claude-haiku-4-5-20251001";
        }
    }

    private PipelineSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<PipelineSettings>(json) ?? new PipelineSettings();
            }
        }
        catch { /* 파일 손상 시 기본값 사용 */ }
        return new PipelineSettings();
    }
}
