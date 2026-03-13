using System.Diagnostics;

namespace LocalCliRunner.Api.Infrastructure;

public class GitCommitService(IConfiguration config, ILogger<GitCommitService> logger)
{
    private static readonly HashSet<string> MdProfiles = ["intake", "spec", "qa"];

    private readonly string _repoPath = Path.GetFullPath(
        config["Git:RepoPath"] ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public async Task AppendAndCommitAsync(string profile, string content)
    {
        if (!MdProfiles.Contains(profile)) return;

        var docsDir  = Path.Combine(_repoPath, "docs");
        var filePath = Path.Combine(docsDir, $"{profile}.md");

        Directory.CreateDirectory(docsDir);

        // Append with timestamp header
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var entry     = $"\n\n---\n## {timestamp}\n\n{content.Trim()}\n";
        await File.AppendAllTextAsync(filePath, entry);

        // Git operations
        var relPath = Path.Combine("docs", $"{profile}.md").Replace('\\', '/');
        await RunGitAsync("add", relPath);
        await RunGitAsync("commit", "-m", $"docs: {profile} updated - {timestamp}");

        if (config["Git:AutoPush"] == "true")
            await RunGitAsync("push");
    }

    private async Task RunGitAsync(params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory       = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            logger.LogWarning("git {Args} exited {Code}: {Stderr}", string.Join(' ', args), proc.ExitCode, stderr);
        else
            logger.LogInformation("git {Args} OK", string.Join(' ', args));
    }
}
