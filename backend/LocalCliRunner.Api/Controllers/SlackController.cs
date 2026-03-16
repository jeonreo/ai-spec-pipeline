using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/slack")]
public class SlackController(SlackService slackService) : ControllerBase
{
    // GET /api/slack/status
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { configured = slackService.IsConfigured });

    // POST /api/slack/extract
    // body: { url: string }
    // response: { text: string, files: [{ name, mimeType, base64 }] }
    [HttpPost("extract")]
    public async Task<IActionResult> Extract([FromBody] SlackExtractRequest req, CancellationToken ct)
    {
        if (!slackService.IsConfigured)
            return BadRequest(new { error = "Slack Bot Token이 설정되지 않았습니다. .env 파일에 Slack__BotToken을 추가하세요." });

        try
        {
            var result = await slackService.ExtractFromUrlAsync(req.Url, ct);
            return Ok(new
            {
                text  = result.Text,
                files = result.Files.Select(f => new
                {
                    name     = f.Name,
                    mimeType = f.MimeType,
                    base64   = Convert.ToBase64String(f.Bytes),
                }).ToArray(),
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record SlackExtractRequest(string Url);
