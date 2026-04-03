using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace LocalCliRunner.Api.Infrastructure;

public class SlackSocketModeService(
    SlackBotService slackBotService,
    SlackWorkflowService workflowService,
    ILogger<SlackSocketModeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!slackBotService.IsSocketModeConfigured)
        {
            logger.LogInformation("Slack Socket Mode is disabled. Configure Slack__BotToken and Slack__AppToken to enable it.");
            return;
        }

        logger.LogInformation("Slack Socket Mode is enabled.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Slack Socket Mode connection failed. Retrying soon.");
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task RunConnectionLoopAsync(CancellationToken ct)
    {
        var socketUri = await slackBotService.OpenSocketModeConnectionAsync(ct);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(socketUri, ct);

        logger.LogInformation("Connected to Slack Socket Mode.");

        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var message = await ReceiveTextAsync(socket, ct);
            if (message is null)
                break;

            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";

            if (type == "hello")
            {
                logger.LogInformation("Slack Socket Mode hello received.");
                continue;
            }

            if (type == "disconnect")
            {
                var reason = root.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() ?? "unknown" : "unknown";
                logger.LogWarning("Slack Socket Mode disconnect requested: {Reason}", reason);
                break;
            }

            if (!root.TryGetProperty("envelope_id", out var envelopeProp))
                continue;

            var envelopeId = envelopeProp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(envelopeId))
                continue;

            switch (type)
            {
                case "slash_commands":
                    {
                        var responseText = await HandleSlashCommandAsync(root, ct);
                        await AckAsync(socket, envelopeId, responseText, ct);
                        break;
                    }
                case "interactive":
                    {
                        await AckAsync(socket, envelopeId, null, ct);
                        if (root.TryGetProperty("payload", out var interactivePayload))
                        {
                            var cloned = interactivePayload.Clone();
                            _ = Task.Run(() => workflowService.HandleInteractionAsync(cloned, CancellationToken.None));
                        }

                        break;
                    }
                case "events_api":
                    {
                        await AckAsync(socket, envelopeId, null, ct);
                        if (root.TryGetProperty("payload", out var eventPayload))
                        {
                            var cloned = eventPayload.Clone();
                            _ = Task.Run(() => workflowService.HandleEventEnvelopeAsync(cloned, CancellationToken.None));
                        }

                        break;
                    }
                default:
                    await AckAsync(socket, envelopeId, null, ct);
                    logger.LogDebug("Ignored Slack Socket Mode envelope type {EnvelopeType}.", type);
                    break;
            }
        }

        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", ct);
    }

    private async Task<string> HandleSlashCommandAsync(JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("payload", out var payload))
            return "Slack slash command payload was missing.";

        var command = payload.TryGetProperty("command", out var commandProp) ? commandProp.GetString() ?? "" : "";
        var userId = payload.TryGetProperty("user_id", out var userIdProp) ? userIdProp.GetString() ?? "" : "";
        var userName = payload.TryGetProperty("user_name", out var userNameProp) ? userNameProp.GetString() ?? "" : "";
        var text = payload.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";

        return await workflowService.HandleSlashCommandAsync(command, userId, userName, text, ct);
    }

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8 * 1024];
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct);

            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task AckAsync(ClientWebSocket socket, string envelopeId, string? text, CancellationToken ct)
    {
        object ackPayload = string.IsNullOrWhiteSpace(text)
            ? new { envelope_id = envelopeId }
            : new
            {
                envelope_id = envelopeId,
                payload = new
                {
                    text,
                    response_type = "ephemeral"
                }
            };

        var json = JsonSerializer.Serialize(ackPayload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }
}
