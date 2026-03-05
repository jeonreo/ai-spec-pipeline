namespace LocalCliRunner.Api.Domain;

public class Job
{
    public required string Id           { get; init; }
    public required string Profile      { get; init; }
    public required string WorkspacePath { get; init; }
    public required DateTime CreatedAt  { get; init; }

    public JobStatus Status         { get; set; } = JobStatus.Queued;
    public string?   OutputContent  { get; set; }
    public string?   OutputFile     { get; set; }
    public string?   Error          { get; set; }

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
