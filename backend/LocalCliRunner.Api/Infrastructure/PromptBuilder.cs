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
        ["design"] = "서론 없이 완전한 HTML 파일만 바로 출력하라. 마크다운 코드블록 없이 <!DOCTYPE html>부터 시작하는 순수 HTML만.",
    };

    private static readonly HashSet<string> PolicyProfiles = ["spec"];

    public async Task<string> BuildAsync(string profile, string inputText)
    {
        var baseMd     = await ReadPromptAsync("base.system.md");
        var skillMd    = await ReadSkillAsync(profile, "SKILL.md");
        var templateMd = await TryReadSkillAsync(profile, "template.md");
        var header     = TaskHeaders.GetValueOrDefault(profile, $"서론 없이 {profile} 문서만 바로 출력하라.");

        var templateSection = templateMd is not null
            ? $"\n\n---\n\n## 출력 템플릿\n\n아래 구조를 그대로 유지하며 `[placeholder]` 항목을 채워라.\n\n{templateMd}"
            : "";

        if (PolicyProfiles.Contains(profile))
        {
            var policyMd = await ReadPromptAsync("policy.md");
            return $"{header}\n\n---\n\n{baseMd}\n\n---\n\n{policyMd}\n\n---\n\n{skillMd}{templateSection}\n\n---\n\n## 입력\n\n{inputText}";
        }

        return $"{header}\n\n---\n\n{baseMd}\n\n---\n\n{skillMd}{templateSection}\n\n---\n\n## 입력\n\n{inputText}";
    }

    public Task<string> ReadPolicyAsync() => ReadPromptAsync("policy.md");

    public string GetVerifyScriptPath(string profile)
    {
        var path = Path.Combine(_promptsDir, "skills", profile, "scripts", "verify.sh");
        return File.Exists(path) ? path : string.Empty;
    }

    private Task<string> ReadSkillAsync(string profile, string filename) =>
        ReadPromptAsync(Path.Combine("skills", profile, filename));

    private async Task<string?> TryReadSkillAsync(string profile, string filename)
    {
        try { return await ReadSkillAsync(profile, filename); }
        catch { return null; }
    }

    private Task<string> ReadPromptAsync(string filename) =>
        File.ReadAllTextAsync(Path.Combine(_promptsDir, filename));
}
