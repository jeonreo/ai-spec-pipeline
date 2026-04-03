using System.Text.Json.Serialization;

namespace LocalCliRunner.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowStatus
{
    Queued,
    RunningStage,
    WaitingApproval,
    WaitingFeedback,
    WaitingAction,
    Completed,
    Failed,
    Interrupted,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowStageStatus
{
    Pending,
    Running,
    Done,
    WaitingApproval,
    WaitingFeedback,
    WaitingAction,
    Approved,
    Failed,
    Interrupted,
}

public static class WorkflowStageNames
{
    public const string Intake = "intake";
    public const string Spec   = "spec";
    public const string Jira   = "jira";

    public static readonly string[] Ordered = [Intake, Spec, Jira];

    public static bool IsValid(string stage) =>
        Ordered.Contains(stage, StringComparer.Ordinal);

    public static string? NextAfter(string stage) =>
        stage switch
        {
            Intake => Spec,
            Spec   => Jira,
            _      => null,
        };
}

public class SlackConversationRef
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string RootMessageTs { get; set; } = "";
    public Dictionary<string, string> StageThreadTs { get; set; } = new(StringComparer.Ordinal);
}

public class WorkflowEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class WorkflowJiraDraft
{
    public string ProjectKey { get; set; } = "";
    public string IssueTypeId { get; set; } = "";
    public string IssueTypeName { get; set; } = "";
}

public class WorkflowJiraResult
{
    public string IssueKey { get; set; } = "";
    public string IssueUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WorkflowStageState
{
    public string Name { get; set; } = "";
    public WorkflowStageStatus Status { get; set; } = WorkflowStageStatus.Pending;
    public string ThreadTs { get; set; } = "";
    public string WorkerMessageTs { get; set; } = "";
    public string ReviewerMessageTs { get; set; } = "";
    public string ApprovalMessageTs { get; set; } = "";
    public string LastInput { get; set; } = "";
    public string OutputPreview { get; set; } = "";
    public string OutputFile { get; set; } = "";
    public string ReviewerOutputFile { get; set; } = "";
    public string ReviewerPreview { get; set; } = "";
    public string ReviewerDecision { get; set; } = "";
    public string ReviewerSummary { get; set; } = "";
    public string LastFeedback { get; set; } = "";
    public string LastError { get; set; } = "";
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class WorkflowState
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "slack";
    public string WorkspaceId { get; set; } = "";
    public string WorkspacePath { get; set; } = "";
    public string RequestText { get; set; } = "";
    public string RequestUserId { get; set; } = "";
    public string RequestUserName { get; set; } = "";
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Queued;
    public string CurrentStage { get; set; } = WorkflowStageNames.Intake;
    public string PendingFeedbackStage { get; set; } = "";
    public string LastError { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public SlackConversationRef Slack { get; set; } = new();
    public WorkflowJiraDraft JiraDraft { get; set; } = new();
    public WorkflowJiraResult? JiraResult { get; set; }
    public Dictionary<string, WorkflowStageState> Stages { get; set; } = CreateDefaultStages();
    public Dictionary<string, string> OutputFiles { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> ProcessedInteractionKeys { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> ProcessedEventKeys { get; set; } = new(StringComparer.Ordinal);

    public static Dictionary<string, WorkflowStageState> CreateDefaultStages() =>
        WorkflowStageNames.Ordered.ToDictionary(
            stage => stage,
            stage => new WorkflowStageState { Name = stage },
            StringComparer.Ordinal);
}
