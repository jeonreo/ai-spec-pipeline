using System.Text.Json;

namespace LocalCliRunner.Api.Infrastructure;

public class WorkflowUserJiraPreference
{
    public string SlackUserId { get; set; } = "";
    public string ProjectKey { get; set; } = "";
    public string IssueTypeId { get; set; } = "";
    public string IssueTypeName { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class WorkflowUserPreferencesService
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private Dictionary<string, WorkflowUserJiraPreference> _jiraPreferences;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public WorkflowUserPreferencesService(IWebHostEnvironment env, IConfiguration config)
    {
        _filePath = config["WorkflowUserPreferencesFile"]
            ?? Path.Combine(env.ContentRootPath, "workflow-user-preferences.json");
        _jiraPreferences = Load();
    }

    public WorkflowUserJiraPreference? GetJiraPreference(string slackUserId)
    {
        if (string.IsNullOrWhiteSpace(slackUserId))
            return null;

        lock (_lock)
        {
            return _jiraPreferences.TryGetValue(slackUserId, out var preference)
                ? Clone(preference)
                : null;
        }
    }

    public void SaveJiraPreference(string slackUserId, string projectKey, string issueTypeId, string issueTypeName)
    {
        if (string.IsNullOrWhiteSpace(slackUserId) ||
            string.IsNullOrWhiteSpace(projectKey) ||
            string.IsNullOrWhiteSpace(issueTypeName))
            return;

        lock (_lock)
        {
            _jiraPreferences[slackUserId] = new WorkflowUserJiraPreference
            {
                SlackUserId = slackUserId,
                ProjectKey = projectKey,
                IssueTypeId = issueTypeId,
                IssueTypeName = issueTypeName,
                UpdatedAt = DateTime.UtcNow,
            };

            Persist();
        }
    }

    private Dictionary<string, WorkflowUserJiraPreference> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, WorkflowUserJiraPreference>(StringComparer.Ordinal);

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, WorkflowUserJiraPreference>>(json)
                ?? new Dictionary<string, WorkflowUserJiraPreference>(StringComparer.Ordinal);

            return new Dictionary<string, WorkflowUserJiraPreference>(data, StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, WorkflowUserJiraPreference>(StringComparer.Ordinal);
        }
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_jiraPreferences, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private static WorkflowUserJiraPreference Clone(WorkflowUserJiraPreference preference) =>
        new()
        {
            SlackUserId = preference.SlackUserId,
            ProjectKey = preference.ProjectKey,
            IssueTypeId = preference.IssueTypeId,
            IssueTypeName = preference.IssueTypeName,
            UpdatedAt = preference.UpdatedAt,
        };
}
