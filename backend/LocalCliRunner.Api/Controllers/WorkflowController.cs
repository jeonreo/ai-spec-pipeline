using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowController(SlackWorkflowService workflowService) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        var items = workflowService.List().Select(workflow => new
        {
            id = workflow.Id,
            source = workflow.Source,
            status = workflow.Status,
            currentStage = workflow.CurrentStage,
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt,
            requestUserName = workflow.RequestUserName,
            requestPreview = workflow.RequestText.Length > 160 ? workflow.RequestText[..160] + "..." : workflow.RequestText,
            jiraIssueKey = workflow.JiraResult?.IssueKey,
            origin = workflow.Origin,
        });

        return Ok(new { items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var workflow = workflowService.Get(id);
        if (workflow is null)
            return NotFound();

        var outputs = await workflowService.ReadOutputsAsync(id, ct);
        return Ok(new { workflow, outputs });
    }

    [HttpPost("{id}/rerun")]
    public async Task<IActionResult> Rerun(string id, [FromBody] WorkflowRerunRequest? request, CancellationToken ct)
    {
        var workflow = workflowService.Get(id);
        if (workflow is null)
            return NotFound();

        var ok = await workflowService.RerunStageAsync(id, request?.Stage, ct);
        return ok ? Ok(new { ok = true }) : BadRequest(new { error = "Invalid workflow stage." });
    }
}

public record WorkflowRerunRequest(string? Stage);
