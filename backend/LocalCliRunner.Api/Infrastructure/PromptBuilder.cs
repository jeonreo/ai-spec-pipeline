namespace LocalCliRunner.Api.Infrastructure;

public class PromptBuilder(IConfiguration config)
{
    private readonly string _promptsDir = Path.GetFullPath(
        config["Prompts:Dir"] ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "prompts"));

    private static readonly Dictionary<string, string> TaskHeaders = new()
    {
        ["intake"] = "서론 없이 intake 문서만 바로 출력하라.",
        ["spec"]   = "서론 없이 spec 문서만 바로 출력하라.",
        ["jira"]   = "서론 없이 JSON만 바로 출력하라. 마크다운 코드블록 없이 순수 JSON만.",
        ["qa"]     = "서론 없이 QA 문서만 바로 출력하라.",
    };

    public async Task<string> BuildAsync(string profile, string inputText)
    {
        var baseMd    = await ReadPromptAsync("base.system.md");
        var stageMd   = await ReadPromptAsync($"{profile}.prompt.md");
        var header    = TaskHeaders.GetValueOrDefault(profile, $"# 작업\n파일: {profile}");

        return $"{header}\n\n---\n\n{baseMd}\n\n---\n\n{stageMd}\n\n---\n\n## 입력\n\n{inputText}";
    }

    private Task<string> ReadPromptAsync(string filename) =>
        File.ReadAllTextAsync(Path.Combine(_promptsDir, filename));
}
