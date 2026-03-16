namespace LocalCliRunner.Api.Infrastructure;

public record TokenUsage(int InputTokens, int OutputTokens);

public interface ICliRunner
{
    Task<CliResult> RunAsync(string promptContent, string workspacePath, string? model = null, IReadOnlyList<string>? imagePaths = null, CancellationToken ct = default);
    Task<TokenUsage?> StreamAsync(string promptContent, string workspacePath, Func<string, Task> onChunk, string? model = null, IReadOnlyList<string>? imagePaths = null, CancellationToken ct = default);
}

public record CliResult(int ExitCode, string Stdout, string Stderr, TokenUsage? Usage = null);
