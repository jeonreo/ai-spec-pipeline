namespace LocalCliRunner.Api.Workspace;

/// <summary>
/// 워크스페이스 디렉토리 내 파일 경로 헬퍼
/// </summary>
public class WorkspaceLayout(string root)
{
    public string InputFile  => Path.Combine(root, "input.txt");
    public string PromptFile => Path.Combine(root, "prompt.txt");
    public string LogFile    => Path.Combine(root, "logs", "run.log");
    public string MetaFile   => Path.Combine(root, "logs", "meta.json");
    public string WorkflowFile => Path.Combine(root, "workflow.json");
    public string SlackFile => Path.Combine(root, "slack.json");
    public string WorkflowEventsFile => Path.Combine(root, "logs", "events.jsonl");

    public string OutputFile(string filename) =>
        Path.Combine(root, "out", filename);
}
