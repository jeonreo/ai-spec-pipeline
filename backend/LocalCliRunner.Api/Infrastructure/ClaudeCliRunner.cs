using System.Diagnostics;
using System.Text;

namespace LocalCliRunner.Api.Infrastructure;

/// <summary>
/// claude CLI를 ProcessStartInfo로 실행하고 stdout을 캡처한다.
/// 전제: claude 명령어가 PATH에 있고 로그인되어 있어야 한다.
/// </summary>
public class ClaudeCliRunner(IConfiguration config, ILogger<ClaudeCliRunner> logger) : ICliRunner
{
    public async Task<CliResult> RunAsync(string promptContent, string workspacePath, CancellationToken ct = default)
    {
        var command    = config["Cli:Command"] ?? "claude";
        var args       = config["Cli:Args"]    ?? "-p -";
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
}
