using LocalCliRunner.Api.Domain;
using LocalCliRunner.Api.Infrastructure;
using LocalCliRunner.Api.Workspace;

namespace LocalCliRunner.Api.Application;

public class RunStageHandler(
    ICliRunner       cliRunner,
    PromptBuilder    promptBuilder,
    WorkspaceManager workspaceManager,
    JobRegistry      jobRegistry,
    PiiTokenizer     piiTokenizer,
    ILogger<RunStageHandler> logger)
{
    private static readonly Dictionary<string, string> OutputFiles = new()
    {
        ["intake"]   = "intake.md",
        ["spec"]     = "spec.md",
        ["jira"]     = "jira.json",
        ["qa"]       = "qa.md",
        ["design"]   = "design.json",
    };


    public RunStageResult Enqueue(RunStageCommand command)
    {
        var jobId         = Job.NewId();
        var workspacePath = workspaceManager.Create(jobId);
        var outputFile    = OutputFiles.GetValueOrDefault(command.Profile, $"{command.Profile}.md");

        var job = new Job
        {
            Id            = jobId,
            Profile       = command.Profile,
            WorkspacePath = workspacePath,
            CreatedAt     = DateTime.UtcNow,
            Status        = JobStatus.Queued,
            OutputFile    = outputFile,
        };

        jobRegistry.Register(job);

        // Fire-and-forget: run in background
        _ = RunAsync(job, command);

        return new RunStageResult(jobId, JobStatus.Queued, workspacePath);
    }

    private async Task RunAsync(Job job, RunStageCommand command)
    {
        var layout = new WorkspaceLayout(job.WorkspacePath);

        try
        {
            job.Status = JobStatus.Running;

            // PII 토큰화 (Claude에 전송 전)
            var (tokenizedInput, piiMap) = piiTokenizer.Tokenize(command.InputText);

            // Write input (원본 저장)
            await File.WriteAllTextAsync(layout.InputFile, command.InputText);

            // Build prompt (토큰화된 입력 사용)
            var prompt = await promptBuilder.BuildAsync(command.Profile, tokenizedInput);
            await File.WriteAllTextAsync(layout.PromptFile, prompt);

            // Run CLI
            var result = await cliRunner.RunAsync(prompt, job.WorkspacePath, command.Model);

            // PII 복원 (출력 후)
            var restoredOutput = piiTokenizer.Detokenize(result.Stdout, piiMap);
            if (command.Profile is "jira" or "design")
                restoredOutput = StripCodeFence(restoredOutput);

            // Write output (복원된 결과 저장)
            var outPath = layout.OutputFile(job.OutputFile!);
            await File.WriteAllTextAsync(outPath, restoredOutput);

            // Write log
            await File.WriteAllTextAsync(layout.LogFile, result.Stderr);

            // Write meta (no sensitive info)
            var meta = $$$"""
                {
                  "createdAt": "{{{job.CreatedAt:O}}}",
                  "profile": "{{{command.Profile}}}",
                  "exitCode": {{{result.ExitCode}}}
                }
                """;
            await File.WriteAllTextAsync(layout.MetaFile, meta);

            job.OutputContent = restoredOutput;
            job.Status        = result.ExitCode == 0 ? JobStatus.Done : JobStatus.Failed;

            if (result.ExitCode != 0)
                job.Error = $"CLI exited with code {result.ExitCode}. stderr: {result.Stderr[..Math.Min(500, result.Stderr.Length)]}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", job.Id);
            job.Status = JobStatus.Failed;
            job.Error  = ex.Message;
        }
    }

    private static string StripCodeFence(string text)
    {
        var s = text.TrimStart();
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline >= 0)
                s = s[(firstNewline + 1)..];
        }

        var trimmed = s.TrimEnd();
        if (trimmed.EndsWith("```", StringComparison.Ordinal))
            s = trimmed[..trimmed.LastIndexOf("```", StringComparison.Ordinal)];

        return s.Trim();
    }
}
