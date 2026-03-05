namespace LocalCliRunner.Api.Workspace;

public class WorkspaceManager(IConfiguration config)
{
    private readonly string _baseDir = Path.GetFullPath(
        config["Workspace:BaseDir"] ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "workspaces"));

    public string Create(string jobId)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var dir       = Path.Combine(_baseDir, "local", $"{timestamp}-{jobId}");

        Directory.CreateDirectory(Path.Combine(dir, "out"));
        Directory.CreateDirectory(Path.Combine(dir, "logs"));

        return dir;
    }

    public IEnumerable<string> ListAll()
    {
        var root = Path.Combine(_baseDir, "local");
        if (!Directory.Exists(root)) return [];
        return Directory.GetDirectories(root)
                        .OrderByDescending(d => d);
    }
}
