using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LocalCliRunner.Api.Infrastructure;

public class SlackBotService(IConfiguration config, IHttpClientFactory httpFactory)
{
    private string BotToken =>
        Environment.GetEnvironmentVariable("Slack__BotToken") ??
        config["Slack:BotToken"] ??
        "";

    private string AppToken =>
        Environment.GetEnvironmentVariable("Slack__AppToken") ??
        config["Slack:AppToken"] ??
        "";

    private string SigningSecret =>
        Environment.GetEnvironmentVariable("Slack__SigningSecret") ??
        config["Slack:SigningSecret"] ??
        "";

    public string PublicBaseUrl =>
        (Environment.GetEnvironmentVariable("App__PublicBaseUrl") ??
         config["App:PublicBaseUrl"] ??
         "").TrimEnd('/');

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BotToken) &&
        !string.IsNullOrWhiteSpace(SigningSecret);

    public bool HasBotToken => !string.IsNullOrWhiteSpace(BotToken);
    public bool HasAppToken => !string.IsNullOrWhiteSpace(AppToken);
    public bool HasSigningSecret => !string.IsNullOrWhiteSpace(SigningSecret);
    public bool IsSocketModeConfigured => HasBotToken && HasAppToken;

    public bool VerifySignature(string rawBody, string? timestamp, string? signature)
    {
        if (string.IsNullOrWhiteSpace(SigningSecret) ||
            string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(signature))
            return false;

        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unixTs))
            return false;

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixTs);
        if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5)
            return false;

        var baseString = $"v0:{timestamp}:{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var expected = $"v0={Convert.ToHexString(hash).ToLowerInvariant()}";

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    public async Task<string> OpenDirectMessageAsync(string userId, CancellationToken ct = default)
    {
        using var doc = await PostJsonAsync("conversations.open", new { users = userId }, ct);
        return doc.RootElement.GetProperty("channel").GetProperty("id").GetString() ?? "";
    }

    public async Task<string> PostMessageAsync(
        string channelId,
        string text,
        IReadOnlyList<object>? blocks = null,
        string? threadTs = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["channel"] = channelId,
            ["text"] = text,
            ["blocks"] = blocks,
        };

        if (!string.IsNullOrWhiteSpace(threadTs))
            payload["thread_ts"] = threadTs;

        using var doc = await PostJsonAsync("chat.postMessage", payload, ct);
        return doc.RootElement.GetProperty("ts").GetString() ?? "";
    }

    public async Task UpdateMessageAsync(
        string channelId,
        string ts,
        string text,
        IReadOnlyList<object>? blocks = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["channel"] = channelId,
            ["ts"] = ts,
            ["text"] = text,
            ["blocks"] = blocks,
        };

        using var _ = await PostJsonAsync("chat.update", payload, ct);
    }

    public async Task OpenViewAsync(string triggerId, object view, CancellationToken ct = default)
    {
        using var _ = await PostJsonAsync("views.open", new { trigger_id = triggerId, view }, ct);
    }

    public async Task UpdateViewAsync(string viewId, string? hash, object view, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["view_id"] = viewId,
            ["view"] = view,
        };

        if (!string.IsNullOrWhiteSpace(hash))
            payload["hash"] = hash;

        using var _ = await PostJsonAsync("views.update", payload, ct);
    }

    public async Task<Uri> OpenSocketModeConnectionAsync(CancellationToken ct = default)
    {
        if (!HasAppToken)
            throw new InvalidOperationException("Slack App Token is not configured.");

        using var doc = await PostJsonWithTokenAsync(AppToken, "apps.connections.open", new { }, ct);
        var url = doc.RootElement.GetProperty("url").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Slack Socket Mode URL was missing from apps.connections.open.");

        return new Uri(url);
    }

    private async Task<JsonDocument> PostJsonAsync(string method, object payload, CancellationToken ct)
    {
        if (!HasBotToken)
            throw new InvalidOperationException("Slack Bot Token is not configured.");

        return await PostJsonWithTokenAsync(BotToken, method, payload, ct);
    }

    private async Task<JsonDocument> PostJsonWithTokenAsync(string token, string method, object payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException($"Slack token for '{method}' is not configured.");

        using var client = httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"https://slack.com/api/{method}", content, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            var error = doc.RootElement.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString()
                : "unknown_error";
            doc.Dispose();
            throw new InvalidOperationException($"Slack API error: {error}");
        }

        return doc;
    }
}
