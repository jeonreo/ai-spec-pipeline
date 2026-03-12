using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LocalCliRunner.Api.Infrastructure;

/// <summary>
/// claude CLI를 ProcessStartInfo로 실행하고 stdout을 캡처한다.
/// 전제: claude 명령어가 PATH에 있고 로그인되어 있어야 한다.
/// </summary>
public class ClaudeCliRunner(IConfiguration config, ILogger<ClaudeCliRunner> logger) : ICliRunner
{
    private string BuildArgs(string? model)
    {
        var effectiveModel = model ?? config["Cli:DefaultModel"] ?? "claude-haiku-4-5-20251001";
        return $"-p --model {effectiveModel} -";
    }

    public async Task<CliResult> RunAsync(string promptContent, string workspacePath, string? model = null, CancellationToken ct = default)
    {
        var command    = config["Cli:Command"] ?? "claude";
        var args       = BuildArgs(model);
        var timeoutSec = int.TryParse(config["Cli:TimeoutSeconds"], out var t) ? t : 120;

        var psi = new ProcessStartInfo
        {
            FileName               = command,
            Arguments              = args,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            WorkingDirectory       = workspacePath,
        };

        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutSb.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderrSb.AppendLine(e.Data); };

        logger.LogInformation("Starting CLI: {Command} {Args}", command, args);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 프롬프트를 stdin으로 전달
        await process.StandardInput.WriteAsync(promptContent);
        process.StandardInput.Close();

        await process.WaitForExitAsync(cts.Token);

        logger.LogInformation("CLI exited with code {ExitCode}", process.ExitCode);

        return new CliResult(process.ExitCode, stdoutSb.ToString().TrimEnd(), stderrSb.ToString().TrimEnd());
    }

    public async Task<TokenUsage?> StreamAsync(string promptContent, string workspacePath, Func<string, Task> onChunk, string? model = null, CancellationToken ct = default)
    {
        var command    = config["Cli:Command"] ?? "claude";
        var effectiveModel = model ?? config["Cli:DefaultModel"] ?? "claude-haiku-4-5-20251001";
        var args       = $"-p --model {effectiveModel} -";
        var timeoutSec = int.TryParse(config["Cli:TimeoutSeconds"], out var t) ? t : 300;

        var psi = new ProcessStartInfo
        {
            FileName               = command,
            Arguments              = args,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            WorkingDirectory       = workspacePath,
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        using var process = new Process { StartInfo = psi };
        process.Start();

        await process.StandardInput.WriteAsync(promptContent);
        process.StandardInput.Close();

        // char 단위로 읽어 즉시 콜백 (줄바꿈 대기 없음)
        var buffer = new char[1024];
        int count;
        while ((count = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length)) > 0
               && !cts.Token.IsCancellationRequested)
        {
            await onChunk(new string(buffer, 0, count));
        }

        await process.WaitForExitAsync(cts.Token);
        logger.LogInformation("Stream CLI exited with code {ExitCode}", process.ExitCode);

        // Claude CLI 스트리밍에서는 토큰 정보를 제공하지 않음 (Vertex AI만 지원)
        return null;
    }
}
