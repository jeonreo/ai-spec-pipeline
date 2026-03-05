namespace LocalCliRunner.Api.Infrastructure;

public interface ICliRunner
{
    Task<CliResult> RunAsync(string promptContent, string workspacePath, CancellationToken ct = default);
    Task StreamAsync(string promptContent, string workspacePath, Func<string, Task> onChunk, CancellationToken ct = default);
}

public record CliResult(int ExitCode, string Stdout, string Stderr);
