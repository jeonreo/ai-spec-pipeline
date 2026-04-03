using System.Text;
using System.Text.Json;
using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/slack")]
public class SlackWorkflowController(
    SlackBotService slackBotService,
    SlackWorkflowService workflowService) : ControllerBase
{
    [HttpPost("commands")]
    [HttpPost("commands/spec")]
    [HttpPost("commands/spec-status")]
    [HttpPost("commands/spec-rerun")]
    [HttpPost("commands/spec-help")]
    public async Task<IActionResult> SlashCommand(CancellationToken ct)
    {
        var rawBody = await ReadRawBodyAsync(Request, ct);
        if (!slackBotService.VerifySignature(
                rawBody,
                Request.Headers["X-Slack-Request-Timestamp"],
                Request.Headers["X-Slack-Signature"]))
            return Unauthorized();

        if (!slackBotService.IsConfigured)
            return BadRequest("Slack workflow configuration is incomplete.");

        var form = await Request.ReadFormAsync(ct);
        var command = form["command"].ToString();
        var text = form["text"].ToString();
        var userId = form["user_id"].ToString();
        var userName = form["user_name"].ToString();

        var message = await workflowService.HandleSlashCommandAsync(command, userId, userName, text, ct);
        return Content(message, "text/plain", Encoding.UTF8);
    }

    [HttpPost("interactions")]
    public async Task<IActionResult> Interactions(CancellationToken ct)
    {
        var rawBody = await ReadRawBodyAsync(Request, ct);
        if (!slackBotService.VerifySignature(
                rawBody,
                Request.Headers["X-Slack-Request-Timestamp"],
                Request.Headers["X-Slack-Signature"]))
            return Unauthorized();

        var form = await Request.ReadFormAsync(ct);
        var payloadJson = form["payload"].ToString();
        if (string.IsNullOrWhiteSpace(payloadJson))
            return Ok();

        using var doc = JsonDocument.Parse(payloadJson);
        var payload = doc.RootElement.Clone();
        _ = Task.Run(() => workflowService.HandleInteractionAsync(payload, CancellationToken.None));
        return Ok();
    }

    [HttpPost("events")]
    public async Task<IActionResult> Events(CancellationToken ct)
    {
        var rawBody = await ReadRawBodyAsync(Request, ct);
        if (!slackBotService.VerifySignature(
                rawBody,
                Request.Headers["X-Slack-Request-Timestamp"],
                Request.Headers["X-Slack-Signature"]))
            return Unauthorized();

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";

        if (type == "url_verification")
        {
            var challenge = root.TryGetProperty("challenge", out var challengeProp) ? challengeProp.GetString() ?? "" : "";
            return Content(challenge, "text/plain", Encoding.UTF8);
        }

        var payload = root.Clone();
        _ = Task.Run(() => workflowService.HandleEventEnvelopeAsync(payload, CancellationToken.None));
        return Ok();
    }

    private static async Task<string> ReadRawBodyAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        request.Body.Position = 0;
        return rawBody;
    }
}
