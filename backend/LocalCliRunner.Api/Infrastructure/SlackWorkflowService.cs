using System.Collections.Concurrent;
using System.Text.Json;
using LocalCliRunner.Api.Domain;
using LocalCliRunner.Api.Workspace;

namespace LocalCliRunner.Api.Infrastructure;

public class SlackWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private sealed record ReviewIssue(string Severity, string Title, string Details);
    private sealed record ReviewData(
        string Decision,
        string Summary,
        IReadOnlyList<string> Strengths,
        IReadOnlyList<ReviewIssue> Issues,
        IReadOnlyList<string> RecommendedChanges);

    private readonly WorkspaceManager workspaceManager;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly SlackBotService slackBotService;
    private readonly JiraService jiraService;
    private readonly WorkflowUserPreferencesService userPreferencesService;
    private readonly ILogger<SlackWorkflowService> logger;
    private readonly ConcurrentDictionary<string, WorkflowState> _workflows = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public SlackWorkflowService(
        WorkspaceManager workspaceManager,
        IServiceScopeFactory scopeFactory,
        SlackBotService slackBotService,
        JiraService jiraService,
        WorkflowUserPreferencesService userPreferencesService,
        ILogger<SlackWorkflowService> logger)
    {
        this.workspaceManager = workspaceManager;
        this.scopeFactory = scopeFactory;
        this.slackBotService = slackBotService;
        this.jiraService = jiraService;
        this.userPreferencesService = userPreferencesService;
        this.logger = logger;

        LoadExisting();

        if (slackBotService.HasBotToken)
            _ = Task.Run(NotifyInterruptedWorkflowsAsync);
    }

    public IReadOnlyList<WorkflowState> List() =>
        _workflows.Values
            .OrderByDescending(w => w.CreatedAt)
            .Select(Clone)
            .ToList();

    public WorkflowState? Get(string workflowId) =>
        _workflows.TryGetValue(workflowId, out var workflow) ? Clone(workflow) : null;

    public async Task<Dictionary<string, string>> ReadOutputsAsync(string workflowId, CancellationToken ct = default)
    {
        if (!_workflows.TryGetValue(workflowId, out var workflow))
            return [];

        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        var layout = new WorkspaceLayout(workflow.WorkspacePath);

        foreach (var stage in WorkflowStageNames.Ordered)
        {
            var fileName = workflow.OutputFiles.GetValueOrDefault(stage) ?? StageProfiles.GetOutputFile(stage);
            var path = layout.OutputFile(fileName);
            if (File.Exists(path))
                outputs[stage] = await File.ReadAllTextAsync(path, ct);
        }

        return outputs;
    }

    public async Task<WorkflowState> StartWorkflowFromSlashAsync(
        string userId,
        string userName,
        string initialText,
        CancellationToken ct = default)
    {
        var workspacePath = workspaceManager.Create($"wf-{Guid.NewGuid().ToString("N")[..6]}");
        var workspaceId = Path.GetFileName(workspacePath);
        var channelId = await slackBotService.OpenDirectMessageAsync(userId, ct);

        var jiraPreference = userPreferencesService.GetJiraPreference(userId);

        var workflow = new WorkflowState
        {
            Id = workspaceId,
            WorkspaceId = workspaceId,
            WorkspacePath = workspacePath,
            RequestText = initialText,
            RequestUserId = userId,
            RequestUserName = userName,
            CurrentStage = WorkflowStageNames.Intake,
            Slack = new SlackConversationRef
            {
                UserId = userId,
                UserName = userName,
                ChannelId = channelId,
            },
            JiraDraft = new WorkflowJiraDraft
            {
                ProjectKey = jiraPreference?.ProjectKey ?? jiraService.DefaultProjectKey,
                IssueTypeId = jiraPreference?.IssueTypeId ?? "",
                IssueTypeName = jiraPreference?.IssueTypeName ?? jiraService.DefaultIssueTypeName,
            },
        };

        _workflows[workflow.Id] = workflow;
        await SaveWorkflowAsync(workflow);
        await AppendEventAsync(workflow, "workflow_created", "", userId, "Slack workflow created");

        var rootTs = await slackBotService.PostMessageAsync(
            channelId,
            "Slack workflow started.",
            BuildRootBlocks(workflow),
            null,
            ct);

        await WithWorkflowLockAsync(workflow.Id, async current =>
        {
            current.Slack.RootMessageTs = rootTs;
            await SaveWorkflowAsync(current);
            await EnsureStageThreadAsync(current, WorkflowStageNames.Intake, ct);
        });

        _ = Task.Run(() => RunStageAsync(workflow.Id, WorkflowStageNames.Intake, null, CancellationToken.None));

        return Clone(workflow);
    }

    public Task<string> QueueSlashSpecAsync(
        string userId,
        string userName,
        string initialText,
        CancellationToken ct = default)
    {
        var trimmed = initialText.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return Task.FromResult("Please include the initial request text.");

        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult("Slack user information was missing.");

        if (!slackBotService.HasBotToken)
            return Task.FromResult("Slack Bot Token is not configured, so the DM workflow cannot start.");

        _ = Task.Run(() => StartWorkflowFromSlashAsync(userId, userName, trimmed, CancellationToken.None));
        return Task.FromResult("Started the workflow in DM.");
    }

    public Task<string> HandleSlashCommandAsync(
        string command,
        string userId,
        string userName,
        string text,
        CancellationToken ct = default) =>
        command.Trim() switch
        {
            "/spec" => QueueSlashSpecAsync(userId, userName, text, ct),
            "/spec-status" => GetStatusForUserAsync(userId, ct),
            "/spec-rerun" => QueueRerunForUserAsync(userId, text, ct),
            "/spec-help" => Task.FromResult(BuildHelpText()),
            _ => Task.FromResult($"Unsupported command: {command}")
        };

    public async Task<string> GetStatusForUserAsync(string userId, CancellationToken ct = default)
    {
        var workflow = FindRelevantWorkflowForUser(userId);
        if (workflow is null)
            return "No workflow found yet. Use /spec <request> to start one.";

        var stageLines = WorkflowStageNames.Ordered
            .Select(stage =>
            {
                var status = workflow.Stages.TryGetValue(stage, out var stageState)
                    ? stageState.Status.ToString()
                    : WorkflowStageStatus.Pending.ToString();
                var current = string.Equals(workflow.CurrentStage, stage, StringComparison.Ordinal)
                    ? " <- current"
                    : "";
                return $"{StageLabel(stage)}: {status}{current}";
            });

        var jiraLine = workflow.JiraResult is null
            ? $"Jira target: {workflow.JiraDraft.ProjectKey}/{workflow.JiraDraft.IssueTypeName}"
            : $"Jira: {workflow.JiraResult.IssueKey} ({workflow.JiraResult.IssueUrl})";

        var consoleUrl = GetWorkflowConsoleUrl(workflow.Id);
        var consoleLine = string.IsNullOrWhiteSpace(consoleUrl)
            ? ""
            : $"\nWeb Console: {consoleUrl}";

        return
            $"Workflow: {workflow.Id}\n" +
            $"Status: {workflow.Status}\n" +
            $"Current stage: {workflow.CurrentStage}\n" +
            $"{jiraLine}\n" +
            string.Join("\n", stageLines) +
            consoleLine;
    }

    public async Task<string> QueueRerunForUserAsync(string userId, string stageInput, CancellationToken ct = default)
    {
        var workflow = FindRelevantWorkflowForUser(userId);
        if (workflow is null)
            return "No workflow found to rerun. Start one with /spec <request> first.";

        if (workflow.Status == WorkflowStatus.RunningStage)
            return $"Workflow {workflow.Id} is already running stage '{workflow.CurrentStage}'.";

        var resolvedStage = ResolveStageInput(stageInput, workflow.CurrentStage);
        if (string.IsNullOrWhiteSpace(resolvedStage))
            return "Unknown stage. Use intake, spec, jira, or leave it empty for the current stage.";

        var queued = await QueueStageRerunAsync(
            workflow.Id,
            resolvedStage,
            "slash-command",
            "Slash command rerun requested",
            ct);

        if (!queued)
            return $"Could not queue a rerun for workflow {workflow.Id}.";

        return $"Queued a rerun for workflow {workflow.Id} at {StageLabel(resolvedStage)}.";
    }

    public async Task<bool> RerunStageAsync(string workflowId, string? stage, CancellationToken ct = default)
        => await QueueStageRerunAsync(workflowId, stage, "web-console", "Manual rerun requested", ct);

    private async Task<bool> QueueStageRerunAsync(
        string workflowId,
        string? stage,
        string actor,
        string summary,
        CancellationToken ct = default)
    {
        if (!_workflows.TryGetValue(workflowId, out var workflow))
            return false;

        var targetStage = string.IsNullOrWhiteSpace(stage) ? workflow.CurrentStage : stage;
        if (string.IsNullOrWhiteSpace(targetStage) || !WorkflowStageNames.IsValid(targetStage))
            return false;

        _ = Task.Run(() => RunStageAsync(workflowId, targetStage, null, CancellationToken.None));
        await AppendEventAsync(workflow, "stage_rerun_requested", targetStage, actor, summary);
        return true;
    }

    public async Task HandleInteractionAsync(JsonElement payload, CancellationToken ct = default)
    {
        var type = payload.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";
        if (type == "view_submission")
        {
            await HandleViewSubmissionAsync(payload, ct);
            return;
        }

        if (type != "block_actions")
            return;

        if (TryGetViewAction(payload, out var workflowId, out var actionId))
        {
            if (actionId == "jira_project_select")
                await HandleJiraProjectChangedAsync(workflowId, payload, ct);
            return;
        }

        if (!TryGetActionValue(payload, out workflowId, out var stage, out actionId, out var dedupeKey))
            return;

        if (!await TryMarkInteractionProcessedAsync(workflowId, dedupeKey))
            return;

        switch (actionId)
        {
            case "approve_stage":
                await HandleApproveAsync(workflowId, stage, ct);
                break;
            case "request_changes_stage":
                await HandleRequestChangesAsync(workflowId, stage, ct);
                break;
            case "skip_stage":
                await HandleSkipAsync(workflowId, stage, ct);
                break;
            case "create_jira":
                _ = Task.Run(() => CreateJiraAsync(workflowId, CancellationToken.None));
                break;
            case "change_jira_settings":
                await OpenJiraSettingsModalAsync(workflowId, payload, ct);
                break;
        }
    }

    public async Task HandleEventEnvelopeAsync(JsonElement payload, CancellationToken ct = default)
    {
        if (!payload.TryGetProperty("event", out var eventEl))
            return;

        var eventType = eventEl.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";
        if (eventType != "message")
            return;

        if (eventEl.TryGetProperty("subtype", out _))
            return;
        if (eventEl.TryGetProperty("bot_id", out _))
            return;

        var channelId = eventEl.TryGetProperty("channel", out var channelProp) ? channelProp.GetString() ?? "" : "";
        var threadTs = eventEl.TryGetProperty("thread_ts", out var threadProp) ? threadProp.GetString() ?? "" : "";
        var userId = eventEl.TryGetProperty("user", out var userProp) ? userProp.GetString() ?? "" : "";
        var text = eventEl.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
        var eventId = payload.TryGetProperty("event_id", out var eventIdProp) ? eventIdProp.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(channelId) ||
            string.IsNullOrWhiteSpace(threadTs) ||
            string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(text))
            return;

        var workflow = _workflows.Values.FirstOrDefault(w =>
            w.Status == WorkflowStatus.WaitingFeedback &&
            !string.IsNullOrWhiteSpace(w.PendingFeedbackStage) &&
            w.Slack.ChannelId == channelId &&
            w.Slack.StageThreadTs.TryGetValue(w.PendingFeedbackStage, out var candidateThreadTs) &&
            candidateThreadTs == threadTs &&
            w.RequestUserId == userId);

        if (workflow is null)
            return;

        if (!await TryMarkEventProcessedAsync(workflow.Id, string.IsNullOrWhiteSpace(eventId) ? $"{channelId}:{threadTs}:{text}" : eventId))
            return;

        _ = Task.Run(() => RunStageAsync(workflow.Id, workflow.PendingFeedbackStage, text, CancellationToken.None));
    }

    private async Task HandleApproveAsync(string workflowId, string stage, CancellationToken ct)
    {
        string? nextStage = null;

        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.Stages.TryGetValue(stage, out var stageState))
                return;

            stageState.Status = WorkflowStageStatus.Approved;
            stageState.LastError = "";
            workflow.PendingFeedbackStage = "";
            workflow.Status = WorkflowStatus.Queued;
            workflow.LastError = "";

            await AppendEventAsync(workflow, "stage_approved", stage, workflow.RequestUserId, "Stage approved");
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);
            await slackBotService.PostMessageAsync(
                workflow.Slack.ChannelId,
                $"{StageLabel(stage)} 승인됨. 다음 단계로 진행합니다.",
                null,
                stageState.ThreadTs,
                ct);

            nextStage = WorkflowStageNames.NextAfter(stage);
        });

        if (!string.IsNullOrWhiteSpace(nextStage))
            _ = Task.Run(() => RunStageAsync(workflowId, nextStage!, null, CancellationToken.None));
    }

    private async Task HandleSkipAsync(string workflowId, string stage, CancellationToken ct)
    {
        if (stage != WorkflowStageNames.Intake)
            return;

        await HandleApproveAsync(workflowId, stage, ct);
    }

    private async Task HandleRequestChangesAsync(string workflowId, string stage, CancellationToken ct)
    {
        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.Stages.TryGetValue(stage, out var stageState))
                return;

            workflow.Status = WorkflowStatus.WaitingFeedback;
            workflow.PendingFeedbackStage = stage;
            stageState.Status = WorkflowStageStatus.WaitingFeedback;
            stageState.LastError = "";
            workflow.LastError = "";

            await AppendEventAsync(workflow, "stage_feedback_requested", stage, workflow.RequestUserId, "Request changes pressed");
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);
            await slackBotService.PostMessageAsync(
                workflow.Slack.ChannelId,
                $"{StageLabel(stage)} 수정 요청을 기다리는 중입니다. 이 스레드에 답글로 변경사항을 남겨 주세요.",
                null,
                stageState.ThreadTs,
                ct);
        });
    }

    private async Task RunStageAsync(string workflowId, string stage, string? feedback, CancellationToken ct)
    {
        StageExecutionRequest executionRequest;

        try
        {
            executionRequest = await BeginStageExecutionAsync(workflowId, stage, feedback, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start stage {Stage} for workflow {WorkflowId}", stage, workflowId);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var stageExecutionService = scope.ServiceProvider.GetRequiredService<StageExecutionService>();
            var result = await stageExecutionService.ExecuteAsync(executionRequest, null, ct);
            if (HasReviewer(stage))
                await CompleteReviewedStageAsync(workflowId, stage, result, ct);
            else
                await CompleteStageAsync(workflowId, stage, result, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stage {Stage} failed for workflow {WorkflowId}", stage, workflowId);
            await FailStageAsync(workflowId, stage, ex.Message, ct);
        }
    }

    private async Task<StageExecutionRequest> BeginStageExecutionAsync(
        string workflowId,
        string stage,
        string? feedback,
        CancellationToken ct)
    {
        StageExecutionRequest request = null!;

        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.Stages.TryGetValue(stage, out var stageState))
                throw new InvalidOperationException($"Unknown workflow stage: {stage}");

            var threadTs = await EnsureStageThreadAsync(workflow, stage, ct);
            var inputText = await BuildStageInputAsync(workflow, stage, feedback, ct);

            workflow.Status = WorkflowStatus.RunningStage;
            workflow.CurrentStage = stage;
            workflow.PendingFeedbackStage = "";
            workflow.LastError = "";
            stageState.Status = WorkflowStageStatus.Running;
            stageState.StartedAt = DateTime.UtcNow;
            stageState.CompletedAt = null;
            stageState.LastFeedback = feedback ?? "";
            stageState.LastInput = inputText;
            stageState.LastError = "";
            stageState.WorkerMessageTs = "";
            stageState.ReviewerMessageTs = "";
            stageState.ApprovalMessageTs = "";

            stageState.ReviewerOutputFile = "";
            stageState.ReviewerPreview = "";
            stageState.ReviewerDecision = "";
            stageState.ReviewerSummary = "";

            request = new StageExecutionRequest(
                stage,
                inputText,
                null,
                null,
                workflow.WorkspacePath,
                workflow.Id);

            await AppendEventAsync(workflow, "stage_started", stage, workflow.RequestUserId, $"{StageLabel(stage)} started");
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);
            await slackBotService.PostMessageAsync(
                workflow.Slack.ChannelId,
                $"{StageLabel(stage)} 실행을 시작합니다.",
                null,
                threadTs,
                ct);
        });

        return request;
    }

    private async Task CompleteStageAsync(
        string workflowId,
        string stage,
        StageExecutionResult result,
        CancellationToken ct)
    {
        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.Stages.TryGetValue(stage, out var stageState))
                return;

            workflow.OutputFiles[stage] = result.OutputFile;
            stageState.OutputFile = result.OutputFile;
            stageState.OutputPreview = CreateStagePreview(stage, result.Output);
            stageState.CompletedAt = DateTime.UtcNow;
            stageState.LastError = "";
            stageState.WorkerMessageTs = "";

            if (stage == WorkflowStageNames.Jira)
            {
                workflow.Status = WorkflowStatus.WaitingAction;
                stageState.Status = WorkflowStageStatus.WaitingAction;
            }
            else
            {
                workflow.Status = WorkflowStatus.WaitingApproval;
                stageState.Status = WorkflowStageStatus.WaitingApproval;
            }

            workflow.LastError = "";

            await AppendEventAsync(workflow, "stage_completed", stage, workflow.RequestUserId, $"{StageLabel(stage)} completed");
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);

            var blocks = BuildStageReadyBlocks(workflow, stage, result.Output, result.Warning);
            var messageTs = await slackBotService.PostMessageAsync(
                workflow.Slack.ChannelId,
                $"{StageLabel(stage)} 결과가 준비되었습니다.",
                blocks,
                stageState.ThreadTs,
                ct);

            stageState.ApprovalMessageTs = messageTs;
            await SaveWorkflowAsync(workflow);
        });
    }

    private async Task CompleteReviewedStageAsync(
        string workflowId,
        string stage,
        StageExecutionResult result,
        CancellationToken ct)
    {
        var reviewProfile = GetReviewProfile(stage)
            ?? throw new InvalidOperationException($"No review profile configured for stage '{stage}'.");
        ReviewData reviewData;
        StageExecutionResult? reviewResult = null;

        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.Stages.TryGetValue(stage, out var stageState))
                return;

            workflow.OutputFiles[stage] = result.OutputFile;
            stageState.OutputFile = result.OutputFile;
            stageState.OutputPreview = CreateStagePreview(stage, result.Output);
            stageState.LastError = "";
            stageState.CompletedAt = null;

            await AppendEventAsync(workflow, "stage_draft_completed", stage, workflow.RequestUserId, $"{StageLabel(stage)} draft completed");
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);

            var workerMessageTs = await slackBotService.PostMessageAsync(
                workflow.Slack.ChannelId,
                $"{StageLabel(stage)} draft is ready.",
                BuildStageDraftBlocks(stage, result.Output, result.Warning),
                stageState.ThreadTs,
                ct);

            stageState.WorkerMessageTs = workerMessageTs;
            await SaveWorkflowAsync(workflow);
        });

        try
        {
            using var scope = scopeFactory.CreateScope();
            var stageExecutionService = scope.ServiceProvider.GetRequiredService<StageExecutionService>();
            var reviewInput = await BuildReviewInputAsync(workflowId, stage, result.Output, ct);
            reviewResult = await stageExecutionService.ExecuteAsync(
                new StageExecutionRequest(
                    reviewProfile,
                    reviewInput,
                    null,
                    null,
                    Get(workflowId)?.WorkspacePath,
                    workflowId),
                null,
                ct);

            reviewData = ParseReview(reviewResult.Output);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Stage} review failed for workflow {WorkflowId}", stage, workflowId);
            reviewData = new ReviewData(
                "review_unavailable",
                $"The {stage} draft was created, but the automated reviewer did not complete successfully.",
                [],
                [new ReviewIssue("medium", "Reviewer unavailable", ex.Message)],
                []);
        }

        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.Stages.TryGetValue(stage, out var stageState))
                return;

            if (reviewResult is not null)
            {
                workflow.OutputFiles[reviewProfile] = reviewResult.OutputFile;
                stageState.ReviewerOutputFile = reviewResult.OutputFile;
            }

            stageState.ReviewerDecision = reviewData.Decision;
            stageState.ReviewerSummary = reviewData.Summary;
            stageState.ReviewerPreview = CreateReviewPreview(reviewData);
            stageState.CompletedAt = DateTime.UtcNow;
            stageState.LastError = "";
            workflow.LastError = "";

            if (stage == WorkflowStageNames.Jira)
            {
                workflow.Status = WorkflowStatus.WaitingAction;
                stageState.Status = WorkflowStageStatus.WaitingAction;
            }
            else
            {
                workflow.Status = WorkflowStatus.WaitingApproval;
                stageState.Status = WorkflowStageStatus.WaitingApproval;
            }

            await AppendEventAsync(workflow, "stage_review_completed", stage, workflow.RequestUserId, reviewData.Decision);
            await AppendEventAsync(workflow, "stage_completed", stage, workflow.RequestUserId, $"{StageLabel(stage)} completed");
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);

            var reviewerMessageTs = await slackBotService.PostMessageAsync(
                workflow.Slack.ChannelId,
                $"{StageReviewerLabel(stage)}: {reviewData.Decision}",
                BuildReviewBlocks(stage, reviewData),
                stageState.ThreadTs,
                ct);
            stageState.ReviewerMessageTs = reviewerMessageTs;

            var approvalMessageTs = await slackBotService.PostMessageAsync(
                workflow.Slack.ChannelId,
                stage == WorkflowStageNames.Jira ? "Jira action is ready." : $"{StageLabel(stage)} approval is ready.",
                BuildStageReadyBlocks(workflow, stage, result.Output, result.Warning),
                stageState.ThreadTs,
                ct);

            stageState.ApprovalMessageTs = approvalMessageTs;
            await SaveWorkflowAsync(workflow);
        });
    }

    private async Task FailStageAsync(string workflowId, string stage, string error, CancellationToken ct)
    {
        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.Stages.TryGetValue(stage, out var stageState))
                return;

            workflow.Status = WorkflowStatus.Failed;
            workflow.CurrentStage = stage;
            workflow.LastError = error;
            stageState.Status = WorkflowStageStatus.Failed;
            stageState.LastError = error;
            stageState.CompletedAt = DateTime.UtcNow;

            await AppendEventAsync(workflow, "stage_failed", stage, workflow.RequestUserId, error);
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);
            await slackBotService.PostMessageAsync(
                workflow.Slack.ChannelId,
                $"{StageLabel(stage)} 실행이 실패했습니다.\n{error}",
                null,
                stageState.ThreadTs,
                ct);
        });
    }

    private async Task CreateJiraAsync(string workflowId, CancellationToken ct)
    {
        WorkflowJiraDraft draft = new();
        string jiraContent = "";
        string specContent = "";

        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.Stages.TryGetValue(WorkflowStageNames.Jira, out var stageState))
                return;

            workflow.Status = WorkflowStatus.RunningStage;
            workflow.CurrentStage = WorkflowStageNames.Jira;
            stageState.Status = WorkflowStageStatus.Running;
            workflow.LastError = "";
            stageState.LastError = "";

            draft = new WorkflowJiraDraft
            {
                ProjectKey = workflow.JiraDraft.ProjectKey,
                IssueTypeId = workflow.JiraDraft.IssueTypeId,
                IssueTypeName = workflow.JiraDraft.IssueTypeName,
            };

            jiraContent = await ReadStageOutputAsync(workflow, WorkflowStageNames.Jira, ct);
            specContent = await ReadStageOutputAsync(workflow, WorkflowStageNames.Spec, ct);

            await AppendEventAsync(workflow, "jira_creation_started", WorkflowStageNames.Jira, workflow.RequestUserId, "Create Jira pressed");
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);
        });

        try
        {
            var jiraJson = ParseJiraJson(jiraContent);
            var issueKey = await jiraService.CreateIssueAsync(new CreateIssueRequest(
                draft.ProjectKey,
                jiraJson.Summary,
                jiraJson.Description,
                jiraJson.AcceptanceCriteria,
                draft.IssueTypeId,
                draft.IssueTypeName,
                specContent));

            var issueUrl = jiraService.IssueUrl(issueKey);

            await WithWorkflowLockAsync(workflowId, async workflow =>
            {
                var stageState = workflow.Stages[WorkflowStageNames.Jira];
                workflow.JiraResult = new WorkflowJiraResult
                {
                    IssueKey = issueKey,
                    IssueUrl = issueUrl,
                    CreatedAt = DateTime.UtcNow,
                };
                workflow.Status = WorkflowStatus.Completed;
                workflow.LastError = "";
                stageState.Status = WorkflowStageStatus.Approved;
                stageState.CompletedAt = DateTime.UtcNow;
                stageState.LastError = "";

                await AppendEventAsync(workflow, "jira_created", WorkflowStageNames.Jira, workflow.RequestUserId, issueKey);
                await SaveWorkflowAsync(workflow);
                await UpdateRootMessageAsync(workflow, ct);
                await slackBotService.PostMessageAsync(
                    workflow.Slack.ChannelId,
                    $"Jira 생성 완료: {issueKey}\n{issueUrl}",
                    null,
                    stageState.ThreadTs,
                    ct);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Jira creation failed for workflow {WorkflowId}", workflowId);

            await WithWorkflowLockAsync(workflowId, async workflow =>
            {
                var stageState = workflow.Stages[WorkflowStageNames.Jira];
                workflow.Status = WorkflowStatus.WaitingAction;
                workflow.LastError = ex.Message;
                stageState.Status = WorkflowStageStatus.WaitingAction;
                stageState.LastError = ex.Message;

                await AppendEventAsync(workflow, "jira_creation_failed", WorkflowStageNames.Jira, workflow.RequestUserId, ex.Message);
                await SaveWorkflowAsync(workflow);
                await UpdateRootMessageAsync(workflow, ct);
                await slackBotService.PostMessageAsync(
                    workflow.Slack.ChannelId,
                    $"Jira 생성 실패: {ex.Message}",
                    null,
                    stageState.ThreadTs,
                    ct);
            });
        }
    }

    private async Task OpenJiraSettingsModalAsync(string workflowId, JsonElement payload, CancellationToken ct)
    {
        if (!_workflows.TryGetValue(workflowId, out var workflow))
            return;

        var triggerId = payload.TryGetProperty("trigger_id", out var triggerProp) ? triggerProp.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(triggerId))
            return;

        var selectedProject = string.IsNullOrWhiteSpace(workflow.JiraDraft.ProjectKey)
            ? jiraService.DefaultProjectKey
            : workflow.JiraDraft.ProjectKey;

        var projects = await jiraService.GetProjectsAsync();
        if (string.IsNullOrWhiteSpace(selectedProject))
            selectedProject = projects.FirstOrDefault()?.Key ?? "";

        var issueTypes = string.IsNullOrWhiteSpace(selectedProject)
            ? new List<JiraIssueType>()
            : await jiraService.GetIssueTypesAsync(selectedProject);

        var selectedTypeId = ResolveIssueTypeId(issueTypes, workflow.JiraDraft.IssueTypeId, workflow.JiraDraft.IssueTypeName);
        var selectedTypeName = issueTypes.FirstOrDefault(t => t.Id == selectedTypeId)?.Name
            ?? workflow.JiraDraft.IssueTypeName;

        await slackBotService.OpenViewAsync(
            triggerId,
            BuildJiraSettingsView(workflowId, projects, issueTypes, selectedProject, selectedTypeId, selectedTypeName),
            ct);
    }

    private async Task HandleJiraProjectChangedAsync(string workflowId, JsonElement payload, CancellationToken ct)
    {
        var view = payload.GetProperty("view");
        var selectedProject = payload.GetProperty("actions")[0].GetProperty("selected_option").GetProperty("value").GetString() ?? "";
        var projects = await jiraService.GetProjectsAsync();
        var issueTypes = string.IsNullOrWhiteSpace(selectedProject)
            ? new List<JiraIssueType>()
            : await jiraService.GetIssueTypesAsync(selectedProject);

        var selectedTypeId = issueTypes.FirstOrDefault()?.Id ?? "";
        var selectedTypeName = issueTypes.FirstOrDefault()?.Name ?? "";

        var viewId = view.GetProperty("id").GetString() ?? "";
        var hash = view.TryGetProperty("hash", out var hashProp) ? hashProp.GetString() : null;

        await slackBotService.UpdateViewAsync(
            viewId,
            hash,
            BuildJiraSettingsView(workflowId, projects, issueTypes, selectedProject, selectedTypeId, selectedTypeName),
            ct);
    }

    private async Task HandleViewSubmissionAsync(JsonElement payload, CancellationToken ct)
    {
        if (!payload.TryGetProperty("view", out var view))
            return;

        var workflowId = view.TryGetProperty("private_metadata", out var metadataProp) ? metadataProp.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(workflowId) || !_workflows.TryGetValue(workflowId, out _))
            return;

        var values = view.GetProperty("state").GetProperty("values");
        var projectKey = values.GetProperty("jira_project").GetProperty("jira_project_select").GetProperty("selected_option").GetProperty("value").GetString() ?? "";
        var issueTypeId = values.GetProperty("jira_issue_type").GetProperty("jira_issue_type_select").GetProperty("selected_option").GetProperty("value").GetString() ?? "";
        var issueTypeName = values.GetProperty("jira_issue_type").GetProperty("jira_issue_type_select").GetProperty("selected_option").GetProperty("text").GetProperty("text").GetString() ?? "";

        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            workflow.JiraDraft.ProjectKey = projectKey;
            workflow.JiraDraft.IssueTypeId = issueTypeId;
            workflow.JiraDraft.IssueTypeName = issueTypeName;
            userPreferencesService.SaveJiraPreference(workflow.RequestUserId, projectKey, issueTypeId, issueTypeName);

            await AppendEventAsync(workflow, "jira_settings_updated", WorkflowStageNames.Jira, workflow.RequestUserId, $"{projectKey}/{issueTypeName}");
            await SaveWorkflowAsync(workflow);
            await UpdateRootMessageAsync(workflow, ct);

            if (workflow.Stages.TryGetValue(WorkflowStageNames.Jira, out var stageState))
            {
                await slackBotService.PostMessageAsync(
                    workflow.Slack.ChannelId,
                    $"Jira settings updated: {projectKey} / {issueTypeName}. Future workflows will reuse this default.",
                    null,
                    stageState.ThreadTs,
                    ct);

                if (!string.IsNullOrWhiteSpace(stageState.ApprovalMessageTs))
                {
                    var jiraOutput = await ReadStageOutputAsync(workflow, WorkflowStageNames.Jira, ct);
                    await slackBotService.UpdateMessageAsync(
                        workflow.Slack.ChannelId,
                        stageState.ApprovalMessageTs,
                        "Jira result is ready.",
                        BuildStageReadyBlocks(workflow, WorkflowStageNames.Jira, jiraOutput, null),
                        ct);
                }
            }
        });
    }

    private async Task<string> EnsureStageThreadAsync(WorkflowState workflow, string stage, CancellationToken ct)
    {
        if (workflow.Slack.StageThreadTs.TryGetValue(stage, out var existingTs) && !string.IsNullOrWhiteSpace(existingTs))
            return existingTs;

        var ts = await slackBotService.PostMessageAsync(
            workflow.Slack.ChannelId,
            $"{StageLabel(stage)} thread opened.",
            BuildStageAnchorBlocks(workflow, stage),
            null,
            ct);

        workflow.Slack.StageThreadTs[stage] = ts;
        if (workflow.Stages.TryGetValue(stage, out var stageState))
            stageState.ThreadTs = ts;

        await SaveWorkflowAsync(workflow);
        return ts;
    }

    private async Task UpdateRootMessageAsync(WorkflowState workflow, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workflow.Slack.ChannelId) || string.IsNullOrWhiteSpace(workflow.Slack.RootMessageTs))
            return;

        await slackBotService.UpdateMessageAsync(
            workflow.Slack.ChannelId,
            workflow.Slack.RootMessageTs,
            "Workflow status updated.",
            BuildRootBlocks(workflow),
            ct);
    }

    private async Task<string> BuildStageInputAsync(WorkflowState workflow, string stage, string? feedback, CancellationToken ct)
    {
        string baseInput = stage switch
        {
            WorkflowStageNames.Intake => workflow.RequestText,
            WorkflowStageNames.Spec => await ReadStageOutputAsync(workflow, WorkflowStageNames.Intake, ct),
            WorkflowStageNames.Jira => await ReadStageOutputAsync(workflow, WorkflowStageNames.Spec, ct),
            _ => workflow.RequestText,
        };

        var reviewerSection = workflow.Stages.TryGetValue(stage, out var stageState)
            && (!string.IsNullOrWhiteSpace(stageState.ReviewerSummary) || !string.IsNullOrWhiteSpace(stageState.ReviewerPreview))
            ? $"\n\n---\n## Latest Reviewer Findings\n\nDecision: {stageState.ReviewerDecision}\n\n{stageState.ReviewerSummary}\n\n{stageState.ReviewerPreview}".TrimEnd()
            : "";

        if (!string.IsNullOrWhiteSpace(feedback))
            return $"{baseInput}{reviewerSection}\n\n---\n## Review Feedback\n\n{feedback.Trim()}";

        return $"{baseInput}{reviewerSection}";
    }

    private async Task<string> BuildReviewInputAsync(string workflowId, string stage, string stageContent, CancellationToken ct)
    {
        var workflow = Get(workflowId);
        if (workflow is null)
            return stageContent;

        var feedbackSection = string.IsNullOrWhiteSpace(workflow.Stages.GetValueOrDefault(stage)?.LastFeedback)
            ? ""
            : $"\n\n## Latest Revision Feedback\n\n{workflow.Stages[stage].LastFeedback}";

        return stage switch
        {
            WorkflowStageNames.Intake =>
                $"## Original Request\n\n{workflow.RequestText}\n\n" +
                $"## Intake Draft\n\n{stageContent}" +
                feedbackSection,
            WorkflowStageNames.Spec => await BuildSpecReviewInputAsync(workflow, stageContent, feedbackSection, ct),
            WorkflowStageNames.Jira => await BuildJiraReviewInputAsync(workflow, stageContent, feedbackSection, ct),
            _ => stageContent + feedbackSection,
        };
    }

    private async Task<string> BuildSpecReviewInputAsync(
        WorkflowState workflow,
        string specContent,
        string feedbackSection,
        CancellationToken ct)
    {
        var intakeContent = await ReadStageOutputAsync(workflow, WorkflowStageNames.Intake, ct);
        return
            $"## Original Request\n\n{workflow.RequestText}\n\n" +
            $"## Intake Output\n\n{intakeContent}\n\n" +
            $"## Spec Draft\n\n{specContent}" +
            feedbackSection;
    }

    private async Task<string> BuildJiraReviewInputAsync(
        WorkflowState workflow,
        string jiraContent,
        string feedbackSection,
        CancellationToken ct)
    {
        var specContent = await ReadStageOutputAsync(workflow, WorkflowStageNames.Spec, ct);
        return
            $"## Original Request\n\n{workflow.RequestText}\n\n" +
            $"## Spec Output\n\n{specContent}\n\n" +
            $"## Jira Draft\n\n{jiraContent}\n\n" +
            $"## Jira Target\n\nProject: {workflow.JiraDraft.ProjectKey}\nIssue Type: {workflow.JiraDraft.IssueTypeName}" +
            feedbackSection;
    }

    private async Task<string> ReadStageOutputAsync(WorkflowState workflow, string stage, CancellationToken ct)
    {
        var fileName = workflow.OutputFiles.GetValueOrDefault(stage) ?? StageProfiles.GetOutputFile(stage);
        var path = new WorkspaceLayout(workflow.WorkspacePath).OutputFile(fileName);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : "";
    }

    private async Task<bool> TryMarkInteractionProcessedAsync(string workflowId, string dedupeKey)
    {
        var processed = false;

        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.ProcessedInteractionKeys.Add(dedupeKey))
                return;

            processed = true;
            await SaveWorkflowAsync(workflow);
        });

        return processed;
    }

    private async Task<bool> TryMarkEventProcessedAsync(string workflowId, string dedupeKey)
    {
        var processed = false;

        await WithWorkflowLockAsync(workflowId, async workflow =>
        {
            if (!workflow.ProcessedEventKeys.Add(dedupeKey))
                return;

            processed = true;
            await SaveWorkflowAsync(workflow);
        });

        return processed;
    }

    private async Task AppendEventAsync(WorkflowState workflow, string type, string stage, string actor, string summary)
    {
        var layout = new WorkspaceLayout(workflow.WorkspacePath);
        var entry = new WorkflowEvent
        {
            Type = type,
            Stage = stage,
            Actor = actor,
            Summary = summary,
        };

        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        await File.AppendAllTextAsync(layout.WorkflowEventsFile, line);
    }

    private async Task SaveWorkflowAsync(WorkflowState workflow)
    {
        workflow.UpdatedAt = DateTime.UtcNow;
        var layout = new WorkspaceLayout(workflow.WorkspacePath);

        await File.WriteAllTextAsync(layout.WorkflowFile, JsonSerializer.Serialize(workflow, JsonOptions));
        await File.WriteAllTextAsync(layout.SlackFile, JsonSerializer.Serialize(workflow.Slack, JsonOptions));
    }

    private void LoadExisting()
    {
        foreach (var dir in workspaceManager.ListAll())
        {
            var layout = new WorkspaceLayout(dir);
            if (!File.Exists(layout.WorkflowFile))
                continue;

            try
            {
                var json = File.ReadAllText(layout.WorkflowFile);
                var workflow = JsonSerializer.Deserialize<WorkflowState>(json) ?? new WorkflowState();
                workflow.WorkspacePath = dir;
                workflow.Id = string.IsNullOrWhiteSpace(workflow.Id) ? Path.GetFileName(dir) : workflow.Id;
                workflow.WorkspaceId = string.IsNullOrWhiteSpace(workflow.WorkspaceId) ? workflow.Id : workflow.WorkspaceId;
                workflow.Stages ??= WorkflowState.CreateDefaultStages();

                foreach (var stage in WorkflowStageNames.Ordered)
                {
                    if (!workflow.Stages.ContainsKey(stage))
                        workflow.Stages[stage] = new WorkflowStageState { Name = stage };
                }

                if (workflow.Status == WorkflowStatus.RunningStage)
                {
                    workflow.Status = WorkflowStatus.Interrupted;
                    if (workflow.Stages.TryGetValue(workflow.CurrentStage, out var stageState))
                        stageState.Status = WorkflowStageStatus.Interrupted;
                }

                _workflows[workflow.Id] = workflow;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load workflow from {Directory}", dir);
            }
        }
    }

    private async Task NotifyInterruptedWorkflowsAsync()
    {
        foreach (var workflow in _workflows.Values.Where(w => w.Status == WorkflowStatus.Interrupted))
        {
            try
            {
                var stage = string.IsNullOrWhiteSpace(workflow.CurrentStage) ? WorkflowStageNames.Intake : workflow.CurrentStage;
                var threadTs = workflow.Slack.StageThreadTs.GetValueOrDefault(stage);
                await slackBotService.PostMessageAsync(
                    workflow.Slack.ChannelId,
                    $"서버 재시작으로 {StageLabel(stage)} 단계가 중단되었습니다. 웹 콘솔에서 재실행할 수 있습니다.",
                    null,
                    string.IsNullOrWhiteSpace(threadTs) ? null : threadTs,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify interrupted workflow {WorkflowId}", workflow.Id);
            }
        }
    }

    private WorkflowState? FindRelevantWorkflowForUser(string userId)
    {
        var ordered = _workflows.Values
            .Where(w => string.Equals(w.RequestUserId, userId, StringComparison.Ordinal))
            .OrderByDescending(w => w.UpdatedAt)
            .ThenByDescending(w => w.CreatedAt)
            .ToList();

        return ordered.FirstOrDefault(w => w.Status is not WorkflowStatus.Completed and not WorkflowStatus.Failed)
            ?? ordered.FirstOrDefault();
    }

    private static string? ResolveStageInput(string stageInput, string currentStage)
    {
        var normalized = (stageInput ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "current")
            return currentStage;

        return normalized switch
        {
            "intake" => WorkflowStageNames.Intake,
            "spec" => WorkflowStageNames.Spec,
            "jira" => WorkflowStageNames.Jira,
            _ when WorkflowStageNames.IsValid(normalized) => normalized,
            _ => null,
        };
    }

    private static string BuildHelpText() =>
        """
        Available commands:
        /spec <request> - Start a new staged workflow in DM
        /spec-status - Show your latest workflow status
        /spec-rerun [intake|spec|jira] - Rerun the current or selected stage
        /spec-help - Show this help message
        """;

    private async Task WithWorkflowLockAsync(string workflowId, Func<WorkflowState, Task> action)
    {
        if (!_workflows.TryGetValue(workflowId, out var workflow))
            return;

        var gate = _locks.GetOrAdd(workflowId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            await action(workflow);
        }
        finally
        {
            gate.Release();
        }
    }

    private static WorkflowState Clone(WorkflowState workflow) =>
        JsonSerializer.Deserialize<WorkflowState>(JsonSerializer.Serialize(workflow))!;

    private static bool HasReviewer(string stage) =>
        !string.IsNullOrWhiteSpace(GetReviewProfile(stage));

    private static string? GetReviewProfile(string stage) =>
        stage switch
        {
            WorkflowStageNames.Intake => "intake-review",
            WorkflowStageNames.Spec => "spec-review",
            WorkflowStageNames.Jira => "jira-review",
            _ => null,
        };

    private static string StageLabel(string stage) =>
        stage switch
        {
            WorkflowStageNames.Intake => "Intake Agent",
            WorkflowStageNames.Spec => "Spec Agent",
            WorkflowStageNames.Jira => "Jira Agent",
            _ => stage,
        };

    private static string StageReviewerLabel(string stage) =>
        stage switch
        {
            WorkflowStageNames.Intake => "Intake Reviewer",
            WorkflowStageNames.Spec => "Spec Reviewer",
            WorkflowStageNames.Jira => "Jira Reviewer",
            _ => $"{stage} reviewer",
        };

    private IReadOnlyList<object> BuildRootBlocks(WorkflowState workflow)
    {
        var lines = WorkflowStageNames.Ordered
            .Select(stage =>
            {
                var status = workflow.Stages.TryGetValue(stage, out var stageState)
                    ? stageState.Status.ToString()
                    : WorkflowStageStatus.Pending.ToString();
                return $"• {StageLabel(stage)}: {status}";
            });

        var header = $"*Slack Workflow*\nStatus: `{workflow.Status}`\nCurrent stage: `{workflow.CurrentStage}`";
        var requestPreview = TrimForSlack(workflow.RequestText, 240);
        var jiraLine = workflow.JiraResult is null
            ? $"Jira target: `{workflow.JiraDraft.ProjectKey}` / `{workflow.JiraDraft.IssueTypeName}`"
            : $"Jira: <{workflow.JiraResult.IssueUrl}|{workflow.JiraResult.IssueKey}>";

        var blocks = new List<object>
        {
            SectionBlock($"{header}\n{jiraLine}\n\n*Request*\n{requestPreview}"),
            SectionBlock(string.Join("\n", lines)),
        };

        var consoleUrl = GetWorkflowConsoleUrl(workflow.Id);
        if (!string.IsNullOrWhiteSpace(consoleUrl))
        {
            blocks.Add(ActionsBlock([
                LinkButton("Open in Web Console", consoleUrl, "open_console_link")
            ]));
        }

        if (!string.IsNullOrWhiteSpace(workflow.LastError))
            blocks.Add(ContextBlock($"Error: {TrimForSlack(workflow.LastError, 180)}"));

        return blocks;
    }

    private IReadOnlyList<object> BuildStageAnchorBlocks(WorkflowState workflow, string stage) =>
    [
        SectionBlock($"*{StageLabel(stage)}*\n이 스레드는 `{stage}` 단계 전용입니다."),
        ContextBlock($"Workflow: {workflow.Id}")
    ];

    private IReadOnlyList<object> BuildStageDraftBlocks(string stage, string output, string? warning)
    {
        var blocks = new List<object>
        {
            SectionBlock($"*{StageLabel(stage)} Draft*\n{CreateStagePreview(stage, output)}"),
        };

        if (!string.IsNullOrWhiteSpace(warning))
            blocks.Add(ContextBlock($"Warning: {TrimForSlack(warning, 180)}"));

        return blocks;
    }

    private IReadOnlyList<object> BuildReviewBlocks(string stage, ReviewData review)
    {
        var blocks = new List<object>
        {
            SectionBlock($"*{StageReviewerLabel(stage)}*\nDecision: `{review.Decision}`\n{TrimForSlack(review.Summary, 240)}"),
        };

        if (review.Strengths.Count > 0)
        {
            blocks.Add(SectionBlock(
                "*Strengths*\n" +
                string.Join("\n", review.Strengths.Take(3).Select(item => $"- {TrimForSlack(item, 120)}"))));
        }

        if (review.Issues.Count > 0)
        {
            blocks.Add(SectionBlock(
                "*Issues*\n" +
                string.Join("\n", review.Issues.Take(4).Select(issue =>
                    $"- [{issue.Severity}] {TrimForSlack(issue.Title, 80)}: {TrimForSlack(issue.Details, 140)}"))));
        }

        if (review.RecommendedChanges.Count > 0)
        {
            blocks.Add(ContextBlock(
                "Recommended changes: " +
                string.Join(" | ", review.RecommendedChanges.Take(3).Select(item => TrimForSlack(item, 80)))));
        }

        return blocks;
    }

    private IReadOnlyList<object> BuildStageReadyBlocks(WorkflowState workflow, string stage, string output, string? warning)
    {
        var blocks = new List<object>
        {
            SectionBlock($"*{StageLabel(stage)} Ready for Review*\n{CreateStagePreview(stage, output)}"),
        };

        if (!string.IsNullOrWhiteSpace(warning))
            blocks.Add(ContextBlock($"Warning: {TrimForSlack(warning, 180)}"));

        if (stage == WorkflowStageNames.Jira)
        {
            var consoleUrl = GetWorkflowConsoleUrl(workflow.Id);
            blocks.Add(SectionBlock($"Jira target: `{workflow.JiraDraft.ProjectKey}` / `{workflow.JiraDraft.IssueTypeName}`"));
            blocks.Add(ContextBlock("Use Change Jira Settings to update this workflow and save your personal default for future workflows."));

            var elements = new List<object>
            {
                ActionButton("Create Jira", "create_jira", $"{workflow.Id}|{stage}", "primary"),
                ActionButton("Request changes", "request_changes_stage", $"{workflow.Id}|{stage}"),
                ActionButton("Change Jira Settings", "change_jira_settings", $"{workflow.Id}|{stage}"),
            };

            if (!string.IsNullOrWhiteSpace(consoleUrl))
                elements.Add(LinkButton("Open in Web Console", consoleUrl, "open_console"));

            blocks.Add(ActionsBlock(elements));
        }
        else
        {
            var elements = new List<object>
            {
                ActionButton("Approve", "approve_stage", $"{workflow.Id}|{stage}", "primary"),
                ActionButton("Request changes", "request_changes_stage", $"{workflow.Id}|{stage}"),
            };

            if (stage == WorkflowStageNames.Intake)
                elements.Add(ActionButton("Skip", "skip_stage", $"{workflow.Id}|{stage}"));

            blocks.Add(ActionsBlock(elements));
        }

        return blocks;
    }

    private string GetWorkflowConsoleUrl(string workflowId)
    {
        if (string.IsNullOrWhiteSpace(slackBotService.PublicBaseUrl))
            return "";

        return $"{slackBotService.PublicBaseUrl}/?workflow={Uri.EscapeDataString(workflowId)}";
    }

    private static string CreateStagePreview(string stage, string output)
    {
        if (stage == WorkflowStageNames.Jira)
        {
            try
            {
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;
                var summary = root.TryGetProperty("summary", out var summaryProp) ? summaryProp.GetString() ?? "" : "";
                var ac = root.TryGetProperty("acceptance_criteria", out var acProp) && acProp.ValueKind == JsonValueKind.Array
                    ? acProp.GetArrayLength()
                    : 0;
                return $"Summary: {TrimForSlack(summary, 120)}\nAcceptance Criteria: {ac} items";
            }
            catch
            {
                return TrimForSlack(output, 320);
            }
        }

        return TrimForSlack(output.Replace("```", "").Trim(), 320);
    }

    private static string CreateReviewPreview(ReviewData review)
    {
        var issueCount = review.Issues.Count;
        return $"Decision: {review.Decision}\nSummary: {TrimForSlack(review.Summary, 160)}\nIssues: {issueCount}";
    }

    private static ReviewData ParseReview(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var decision = root.TryGetProperty("decision", out var decisionProp)
                ? decisionProp.GetString() ?? "revision_requested"
                : "revision_requested";

            var summary = root.TryGetProperty("summary", out var summaryProp)
                ? summaryProp.GetString() ?? ""
                : "";

            var strengths = root.TryGetProperty("strengths", out var strengthsProp) && strengthsProp.ValueKind == JsonValueKind.Array
                ? strengthsProp.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? "")
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList()
                : [];

            var issues = new List<ReviewIssue>();
            if (root.TryGetProperty("issues", out var issuesProp) && issuesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in issuesProp.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var severity = item.TryGetProperty("severity", out var severityProp) ? severityProp.GetString() ?? "medium" : "medium";
                    var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
                    var details = item.TryGetProperty("details", out var detailsProp) ? detailsProp.GetString() ?? "" : "";
                    issues.Add(new ReviewIssue(severity, title, details));
                }
            }

            var recommendedChanges = root.TryGetProperty("recommended_changes", out var changesProp) && changesProp.ValueKind == JsonValueKind.Array
                ? changesProp.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? "")
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList()
                : [];

            if (string.IsNullOrWhiteSpace(summary))
                summary = issues.Count > 0
                    ? "The reviewer found issues that should be addressed before moving forward."
                    : "The reviewer did not report any major issues.";

            return new ReviewData(decision, summary, strengths, issues, recommendedChanges);
        }
        catch
        {
            return new ReviewData(
                "review_unavailable",
                "The automated review output could not be parsed.",
                [],
                [],
                []);
        }
    }

    private static (string Summary, Dictionary<string, string> Description, List<string> AcceptanceCriteria) ParseJiraJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var description = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in descProp.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    description[prop.Name] = prop.Value.GetString() ?? "";
            }
        }

        var ac = new List<string>();
        if (root.TryGetProperty("acceptance_criteria", out var acProp) && acProp.ValueKind == JsonValueKind.Array)
        {
            ac.AddRange(acProp.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? ""));
        }
        else if (root.TryGetProperty("acceptanceCriteria", out acProp) && acProp.ValueKind == JsonValueKind.Array)
        {
            ac.AddRange(acProp.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? ""));
        }

        return (
            root.TryGetProperty("summary", out var summaryProp) ? summaryProp.GetString() ?? "" : "",
            description,
            ac);
    }

    private static string ResolveIssueTypeId(
        IEnumerable<JiraIssueType> issueTypes,
        string issueTypeId,
        string issueTypeName)
    {
        if (!string.IsNullOrWhiteSpace(issueTypeId) && issueTypes.Any(t => t.Id == issueTypeId))
            return issueTypeId;

        if (!string.IsNullOrWhiteSpace(issueTypeName))
        {
            var byName = issueTypes.FirstOrDefault(t => t.Name.Equals(issueTypeName, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
                return byName.Id;
        }

        return issueTypes.FirstOrDefault()?.Id ?? "";
    }

    private static bool TryGetActionValue(
        JsonElement payload,
        out string workflowId,
        out string stage,
        out string actionId,
        out string dedupeKey)
    {
        workflowId = "";
        stage = "";
        actionId = "";
        dedupeKey = "";

        if (!payload.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array || actions.GetArrayLength() == 0)
            return false;

        var action = actions[0];
        actionId = action.TryGetProperty("action_id", out var actionIdProp) ? actionIdProp.GetString() ?? "" : "";
        var value = action.TryGetProperty("value", out var valueProp) ? valueProp.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;

        workflowId = parts[0];
        stage = parts[1];

        var userId = payload.TryGetProperty("user", out var userProp) && userProp.TryGetProperty("id", out var idProp)
            ? idProp.GetString() ?? ""
            : "";
        var actionTs = action.TryGetProperty("action_ts", out var tsProp) ? tsProp.GetString() ?? "" : "";
        dedupeKey = $"{actionId}:{actionTs}:{userId}";
        return true;
    }

    private static bool TryGetViewAction(JsonElement payload, out string workflowId, out string actionId)
    {
        workflowId = "";
        actionId = "";

        if (!payload.TryGetProperty("view", out var view))
            return false;
        if (!view.TryGetProperty("private_metadata", out var metadataProp))
            return false;
        if (!payload.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array || actions.GetArrayLength() == 0)
            return false;

        workflowId = metadataProp.GetString() ?? "";
        actionId = actions[0].TryGetProperty("action_id", out var actionIdProp) ? actionIdProp.GetString() ?? "" : "";
        return !string.IsNullOrWhiteSpace(workflowId);
    }

    private static object BuildJiraSettingsView(
        string workflowId,
        IReadOnlyList<JiraProject> projects,
        IReadOnlyList<JiraIssueType> issueTypes,
        string selectedProject,
        string selectedIssueTypeId,
        string selectedIssueTypeName)
    {
        var projectOptions = projects
            .Take(100)
            .Select(project => (object)new
            {
                text = new { type = "plain_text", text = $"{project.Key} - {project.Name}" },
                value = project.Key,
            })
            .ToArray();

        var issueTypeOptions = issueTypes
            .Take(100)
            .Select(issueType => (object)new
            {
                text = new { type = "plain_text", text = issueType.Name },
                value = issueType.Id,
            })
            .ToArray();

        var initialProject = projects.FirstOrDefault(p => p.Key == selectedProject);
        var initialIssueType = issueTypes.FirstOrDefault(t => t.Id == selectedIssueTypeId)
            ?? issueTypes.FirstOrDefault(t => t.Name.Equals(selectedIssueTypeName, StringComparison.OrdinalIgnoreCase));

        return new
        {
            type = "modal",
            callback_id = "jira_settings_modal",
            private_metadata = workflowId,
            title = new { type = "plain_text", text = "Jira settings" },
            submit = new { type = "plain_text", text = "Save" },
            close = new { type = "plain_text", text = "Cancel" },
            blocks = new object[]
            {
                new
                {
                    type = "input",
                    block_id = "jira_project",
                    dispatch_action = true,
                    label = new { type = "plain_text", text = "Project" },
                    element = new
                    {
                        type = "static_select",
                        action_id = "jira_project_select",
                        options = projectOptions,
                        initial_option = initialProject is null
                            ? null
                            : new
                            {
                                text = new { type = "plain_text", text = $"{initialProject.Key} - {initialProject.Name}" },
                                value = initialProject.Key,
                            },
                    },
                },
                new
                {
                    type = "input",
                    block_id = "jira_issue_type",
                    label = new { type = "plain_text", text = "Issue type" },
                    element = new
                    {
                        type = "static_select",
                        action_id = "jira_issue_type_select",
                        options = issueTypeOptions,
                        initial_option = initialIssueType is null
                            ? null
                            : new
                            {
                                text = new { type = "plain_text", text = initialIssueType.Name },
                                value = initialIssueType.Id,
                            },
                    },
                },
            },
        };
    }

    private static object SectionBlock(string text) =>
        new
        {
            type = "section",
            text = new { type = "mrkdwn", text },
        };

    private static object ContextBlock(string text) =>
        new
        {
            type = "context",
            elements = new object[]
            {
                new { type = "mrkdwn", text },
            },
        };

    private static object ActionsBlock(IEnumerable<object> elements) =>
        new
        {
            type = "actions",
            elements = elements.ToArray(),
        };

    private static object ActionButton(string text, string actionId, string value, string? style = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = "button",
            ["text"] = new { type = "plain_text", text },
            ["action_id"] = actionId,
            ["value"] = value,
        };

        if (!string.IsNullOrWhiteSpace(style))
            payload["style"] = style;

        return payload;
    }

    private static object LinkButton(string text, string url, string actionId) =>
        new
        {
            type = "button",
            text = new { type = "plain_text", text },
            action_id = actionId,
            url,
        };

    private static string TrimForSlack(string text, int maxLength)
    {
        var normalized = text.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxLength)
            return normalized;

        return normalized[..maxLength] + "...";
    }
}
