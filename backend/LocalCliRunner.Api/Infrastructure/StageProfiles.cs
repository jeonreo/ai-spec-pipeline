namespace LocalCliRunner.Api.Infrastructure;

public static class StageProfiles
{
    public static readonly HashSet<string> ValidProfiles = new(StringComparer.Ordinal)
    {
        "intake",
        "intake-review",
        "spec",
        "spec-review",
        "jira",
        "jira-review",
        "qa",
        "design",
        "code-analysis-be",
        "code-analysis-fe",
        "patch",
        "learn",
    };

    public static readonly Dictionary<string, string> OutputFiles = new(StringComparer.Ordinal)
    {
        ["intake"]           = "intake.md",
        ["intake-review"]    = "intake-review.json",
        ["spec"]             = "spec.md",
        ["spec-review"]      = "spec-review.json",
        ["jira"]             = "jira.json",
        ["jira-review"]      = "jira-review.json",
        ["qa"]               = "qa.md",
        ["design"]           = "design.json",
        ["code-analysis-be"] = "code-analysis-be.md",
        ["code-analysis-fe"] = "code-analysis-fe.md",
        ["patch"]            = "patch.json",
        ["learn"]            = "learn.json",
    };

    public static string GetOutputFile(string profile) =>
        OutputFiles.GetValueOrDefault(profile, $"{profile}.md");

    public static bool ExpectsJsonArray(string profile) =>
        profile is "patch" or "learn";

    public static bool ExpectsJsonObject(string profile) =>
        profile is "jira" or "design" or "intake-review" or "spec-review" or "jira-review";
}
