namespace LocalCliRunner.Api.Infrastructure;

public class PromptBuilder(IConfiguration config)
{
    private readonly string _promptsDir = Path.GetFullPath(
        config["Prompts:Dir"] ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "prompts"));

    private static readonly Dictionary<string, string> TaskHeaders = new()
    {
        ["intake"] = "Output only the intake document.",
        ["intake-review"] = "Output only valid JSON without markdown fences or commentary.",
        ["spec"] = "Output only the decision spec document.",
        ["spec-review"] = "Output only valid JSON without markdown fences or commentary.",
        ["jira"] = "Output only valid JSON without markdown fences or commentary.",
        ["jira-review"] = "Output only valid JSON without markdown fences or commentary.",
        ["qa"] = "Output only the QA document.",
        ["design"] = "Output only a valid Design Package v1 JSON object without markdown fences, HTML, or commentary.",
        ["learn"] = "Output only valid JSON without markdown fences or commentary.",
    };

    private static readonly HashSet<string> PolicyProfiles = ["spec", "spec-review", "jira", "jira-review"];
    private static readonly HashSet<string> BeArchProfiles = ["spec", "spec-review", "code-analysis-be", "patch"];
    private static readonly HashSet<string> FeArchProfiles = ["spec", "spec-review", "code-analysis-fe", "patch"];

    private static readonly Dictionary<string, string> AgentProfiles = new()
    {
        ["intake"] = "pm",
        ["intake-review"] = "pm",
        ["spec"] = "pm",
        ["spec-review"] = "pm",
        ["jira"] = "pm",
        ["jira-review"] = "pm",
        ["qa"] = "qa-eng",
        ["design"] = "pd",
        ["code-analysis-be"] = "be-dev",
        ["code-analysis-fe"] = "fe-dev",
        ["patch"] = "fe-dev",
    };

    public async Task<string> BuildAsync(string profile, string inputText)
    {
        var baseMd = await ReadPromptAsync("base.system.md");
        var skillMd = await ReadSkillAsync(profile, "SKILL.md");
        var header = TaskHeaders.GetValueOrDefault(profile, $"Output only the {profile} document.");

        var agentSection = string.Empty;
        if (AgentProfiles.TryGetValue(profile, out var agentName))
        {
            var agentMd = await TryReadAgentAsync(agentName);
            if (agentMd is not null)
                agentSection = $"\n\n---\n\n{agentMd}";
        }

        if (profile == "policy-update")
        {
            var policyMd = await ReadPromptAsync("policy.md");
            return $"{header}\n\n---\n\n{baseMd}{agentSection}\n\n---\n\n{skillMd}\n\n---\n\n## Current Policy\n\n{policyMd}\n\n---\n\n## Requested Changes\n\n{inputText}";
        }

        var templateMd = await TryReadSkillAsync(profile, "template.md");
        var templateSection = templateMd is not null
            ? $"\n\n---\n\n## Output Template Reference\nUse the following structure as the reference schema for your output.\n\n{templateMd}"
            : string.Empty;

        var architectureSection = string.Empty;
        if (BeArchProfiles.Contains(profile) || FeArchProfiles.Contains(profile))
        {
            var parts = new List<string>();
            if (BeArchProfiles.Contains(profile))
            {
                var beArch = await TryReadContextAsync("be-architecture.md");
                if (beArch is not null)
                    parts.Add(beArch);
            }

            if (FeArchProfiles.Contains(profile))
            {
                var feArch = await TryReadContextAsync("fe-architecture.md");
                if (feArch is not null)
                    parts.Add(feArch);
            }

            if (parts.Count > 0)
                architectureSection = $"\n\n---\n\n## Codebase Architecture\n\n{string.Join("\n\n", parts)}";
        }

        if (PolicyProfiles.Contains(profile))
        {
            var policyMd = await ReadPromptAsync("policy.md");
            return $"{header}\n\n---\n\n{baseMd}{agentSection}\n\n---\n\n{policyMd}{architectureSection}\n\n---\n\n{skillMd}{templateSection}\n\n---\n\n## Input\n\n{inputText}";
        }

        return $"{header}\n\n---\n\n{baseMd}{agentSection}{architectureSection}\n\n---\n\n{skillMd}{templateSection}\n\n---\n\n## Input\n\n{inputText}";
    }

    public Task<string> ReadPolicyAsync() => ReadPromptAsync("policy.md");

    public string GetPolicyPath() => Path.Combine(_promptsDir, "policy.md");

    public string GetSkillPath(string profile) =>
        Path.Combine(_promptsDir, "skills", profile, "SKILL.md");

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

    private async Task<string?> TryReadAgentAsync(string agentName)
    {
        try
        {
            return await File.ReadAllTextAsync(Path.Combine(_promptsDir, "agents", $"{agentName}.md"));
        }
        catch
        {
            return null;
        }
    }

    private Task<string> ReadPromptAsync(string filename) =>
        File.ReadAllTextAsync(Path.Combine(_promptsDir, filename));

    private async Task<string?> TryReadContextAsync(string filename)
    {
        try
        {
            return await File.ReadAllTextAsync(Path.Combine(_promptsDir, "context", filename));
        }
        catch
        {
            return null;
        }
    }
}
