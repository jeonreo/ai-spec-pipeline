namespace LocalCliRunner.Api.Infrastructure;

public interface ICliRunner
{
    Task<CliResult> RunAsync(string promptContent, string workspacePath, CancellationToken ct = default);
}

public record CliResult(int ExitCode, string Stdout, string Stderr);
