using LocalCliRunner.Api.Domain;

namespace LocalCliRunner.Api.Application;

public record RunStageResult(
    string    JobId,
    JobStatus Status,
    string    WorkspacePath,
    string?   OutputContent = null,
    string?   Error         = null
);
