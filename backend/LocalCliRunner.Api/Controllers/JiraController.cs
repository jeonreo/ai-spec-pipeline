using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/jira")]
public class JiraController(JiraService jiraService) : ControllerBase
{
    // GET /api/jira/status  — 연동 설정 여부 확인
    [HttpGet("status")]
    public IActionResult Status() =>
        Ok(new { configured = jiraService.IsConfigured });

    // GET /api/jira/projects
    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        if (!jiraService.IsConfigured)
            return BadRequest(new { error = "Jira 연동 설정이 없습니다. (BaseUrl / Email / ApiToken)" });
        try
        {
            return Ok(await jiraService.GetProjectsAsync());
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = $"Jira API 오류: {ex.Message}" });
        }
    }

    // GET /api/jira/issuetypes/{projectKey}
    [HttpGet("issuetypes/{projectKey}")]
    public async Task<IActionResult> GetIssueTypes(string projectKey)
    {
        if (!jiraService.IsConfigured)
            return BadRequest(new { error = "Jira 연동 설정이 없습니다." });
        try
        {
            return Ok(await jiraService.GetIssueTypesAsync(projectKey));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = $"Jira API 오류: {ex.Message}" });
        }
    }

    // POST /api/jira/create
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateIssueRequest request)
    {
        if (!jiraService.IsConfigured)
            return BadRequest(new { error = "Jira 연동 설정이 없습니다." });
        try
        {
            var key = await jiraService.CreateIssueAsync(request);
            var url = $"{jiraService.IssueUrl(key)}";
            return Ok(new { key, url });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = $"Jira API 오류: {ex.Message}" });
        }
    }
}
