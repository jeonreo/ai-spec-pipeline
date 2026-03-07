namespace LocalCliRunner.Api.Infrastructure;

public class PromptBuilder(IConfiguration config)
{
    private readonly string _promptsDir = Path.GetFullPath(
        config["Prompts:Dir"] ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "prompts"));

    private static readonly Dictionary<string, string> TaskHeaders = new()
    {
        ["intake"] = "Output only the intake document.",
        ["spec"] = "Output only the decision spec document.",
        ["jira"] = "Output only valid JSON without markdown fences or commentary.",
        ["qa"] = "Output only the QA document.",
        ["design"] = "Output only a valid Design Package v1 JSON object without markdown fences, HTML, or commentary.",
    };

    private static readonly HashSet<string> PolicyProfiles = ["spec"];

    public async Task<string> BuildAsync(string profile, string inputText)
    {
        var baseMd = await ReadPromptAsync("base.system.md");
        var skillMd = await ReadSkillAsync(profile, "SKILL.md");
        var templateMd = await TryReadSkillAsync(profile, "template.md");
        var header = TaskHeaders.GetValueOrDefault(profile, $"Output only the {profile} document.");

        var templateSection = templateMd is not null
            ? $"\n\n---\n\n## Output Template Reference\nUse the following structure as the reference schema for your output.\n\n{templateMd}"
            : string.Empty;

        if (PolicyProfiles.Contains(profile))
        {
            var policyMd = await ReadPromptAsync("policy.md");
            return $"{header}\n\n---\n\n{baseMd}\n\n---\n\n{policyMd}\n\n---\n\n{skillMd}{templateSection}\n\n---\n\n## Input\n\n{inputText}";
        }

        return $"{header}\n\n---\n\n{baseMd}\n\n---\n\n{skillMd}{templateSection}\n\n---\n\n## Input\n\n{inputText}";
    }

    public Task<string> ReadPolicyAsync() => ReadPromptAsync("policy.md");

    public string GetVerifyScriptPath(string profile)
    {
        var path = Path.Combine(_promptsDir, "skills", profile, "scripts", "verify.sh");
        return File.Exists(path) ? path : string.Empty;
    }

    public string GetStyleInjectPath(string profile)
    {
        var path = Path.Combine(_promptsDir, "skills", profile, "assets", "style.css");
        return File.Exists(path) ? path : string.Empty;
    }

    private Task<string> ReadSkillAsync(string profile, string filename) =>
        ReadPromptAsync(Path.Combine("skills", profile, filename));

    private async Task<string?> TryReadSkillAsync(string profile, string filename)
    {
        try
        {
            return await ReadSkillAsync(profile, filename);
        }
        catch
        {
            return null;
        }
    }

    private Task<string> ReadPromptAsync(string filename) =>
        File.ReadAllTextAsync(Path.Combine(_promptsDir, filename));
}
