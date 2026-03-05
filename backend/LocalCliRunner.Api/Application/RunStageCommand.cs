namespace LocalCliRunner.Api.Application;

public record RunStageCommand(
    string InputText,
    string Profile   // intake | spec | jira | qa
);
