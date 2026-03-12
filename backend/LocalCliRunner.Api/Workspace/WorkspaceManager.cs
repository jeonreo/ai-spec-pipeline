namespace LocalCliRunner.Api.Workspace;

public class WorkspaceManager(IConfiguration config)
{
    private readonly string _baseDir = Path.GetFullPath(
        config["Workspace:BaseDir"] ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "workspaces"));
    private string LocalRoot => Path.Combine(_baseDir, "local");

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
        var root = LocalRoot;
        if (!Directory.Exists(root)) return [];
        return Directory.GetDirectories(root)
                        .OrderByDescending(d => d);
    }

    /// <summary>
    /// id로 workspace 디렉토리를 직접 조회 (전체 스캔 없이).
    /// </summary>
    public string? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || Path.GetFileName(id) != id) return null;
        var target = Path.GetFullPath(Path.Combine(LocalRoot, id));
        return Directory.Exists(target) ? target : null;
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (Path.GetFileName(id) != id) return false;

        var root = Path.GetFullPath(LocalRoot);
        var target = Path.GetFullPath(Path.Combine(root, id));

        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!Directory.Exists(target))
            return false;

        Directory.Delete(target, recursive: true);
        return true;
    }
}
