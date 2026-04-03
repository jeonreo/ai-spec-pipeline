using LocalCliRunner.Api.Domain;
using LocalCliRunner.Api.Infrastructure;
using LocalCliRunner.Api.Workspace;

namespace LocalCliRunner.Api.Application;

public class RunStageHandler(
    StageExecutionService stageExecutionService,
    WorkspaceManager workspaceManager,
    JobRegistry jobRegistry,
    GitCommitService gitCommitService,
    ILogger<RunStageHandler> logger)
{
    private static readonly Dictionary<string, string> OutputFiles = new()
    {
        ["intake"] = "intake.md",
        ["spec"] = "spec.md",
        ["jira"] = "jira.json",
        ["qa"] = "qa.md",
        ["design"] = "design.json",
    };

    public RunStageResult Enqueue(RunStageCommand command)
    {
        var jobId = Job.NewId();
        var workspacePath = workspaceManager.Create(jobId);
        var outputFile = OutputFiles.GetValueOrDefault(command.Profile, $"{command.Profile}.md");

        var job = new Job
        {
            Id = jobId,
            Profile = command.Profile,
            WorkspacePath = workspacePath,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Queued,
            OutputFile = outputFile,
        };

        jobRegistry.Register(job);
        _ = RunAsync(job, command);

        return new RunStageResult(jobId, JobStatus.Queued, workspacePath);
    }

    private async Task RunAsync(Job job, RunStageCommand command)
    {
        var layout = new WorkspaceLayout(job.WorkspacePath);

        try
        {
            job.Status = JobStatus.Running;

            var result = await stageExecutionService.ExecuteAsync(
                new StageExecutionRequest(
                    command.Profile,
                    command.InputText,
                    null,
                    null,
                    job.WorkspacePath,
                    job.Id));

            _ = gitCommitService.AppendAndCommitAsync(command.Profile, result.Output);

            var meta = $$$"""
                {
                  "createdAt": "{{{job.CreatedAt:O}}}",
                  "profile": "{{{command.Profile}}}",
                  "warning": {{{JsonValue(result.Warning)}}}
                }
                """;
            await File.WriteAllTextAsync(layout.MetaFile, meta);

            job.Status = JobStatus.Done;
            job.OutputFile = result.OutputFile;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", job.Id);
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
        }
    }

    private static string JsonValue(string? value) =>
        value is null ? "null" : $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
