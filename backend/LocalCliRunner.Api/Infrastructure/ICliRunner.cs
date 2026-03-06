namespace LocalCliRunner.Api.Infrastructure;

public interface ICliRunner
{
    Task<CliResult> RunAsync(string promptContent, string workspacePath, string? model = null, CancellationToken ct = default);
    Task StreamAsync(string promptContent, string workspacePath, Func<string, Task> onChunk, string? model = null, CancellationToken ct = default);
}

public record CliResult(int ExitCode, string Stdout, string Stderr);
