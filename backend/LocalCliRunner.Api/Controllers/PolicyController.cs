using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/policy")]
public class PolicyController(
    ICliRunner cliRunner,
    PromptBuilder promptBuilder,
    GitCommitService gitCommitService,
    ILogger<PolicyController> logger) : ControllerBase
{
    public record PolicyUpdateRequest(string Decisions);

    [HttpPost("update")]
    public async Task<IActionResult> Update([FromBody] PolicyUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Decisions))
            return BadRequest(new { error = "결정사항이 비어있습니다." });

        var tempDir = Path.Combine(Path.GetTempPath(), $"policy-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var prompt = await promptBuilder.BuildAsync("policy-update", req.Decisions);
            var result = await cliRunner.RunAsync(prompt, tempDir, null);

            if (result.ExitCode != 0)
            {
                logger.LogWarning("policy-update AI 실패: {Stderr}", result.Stderr);
                return StatusCode(502, new { error = result.Stderr });
            }

            var policyPath = promptBuilder.GetPolicyPath();
            await System.IO.File.WriteAllTextAsync(policyPath, result.Stdout.Trim());

            await gitCommitService.CommitFileAsync("prompts/policy.md", $"docs: policy.md updated - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return Ok(new { updated = true });
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
