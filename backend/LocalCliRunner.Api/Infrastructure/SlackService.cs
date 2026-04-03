using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LocalCliRunner.Api.Infrastructure;

public record SlackFile(string Name, string MimeType, byte[] Bytes);
public record SlackExtractResult(string Text, IReadOnlyList<SlackFile> Files);

/// <summary>
/// Slack Web API 클라이언트.
/// conversations.history / conversations.replies 로 메시지+스레드 수집,
/// files 는 url_private_download 로 다운로드.
/// 인증: Bot Token (xoxb-...) — SLACK__BOTTOKEN 환경변수 또는 appsettings.json Slack:BotToken
/// </summary>
public class SlackImportService(IConfiguration config, IHttpClientFactory httpFactory)
{
    private string BotToken =>
        Environment.GetEnvironmentVariable("Slack__BotToken") ??
        config["Slack:BotToken"] ?? "";

    public bool IsConfigured => !string.IsNullOrEmpty(BotToken);

    public async Task<SlackExtractResult> ExtractFromUrlAsync(string slackUrl, CancellationToken ct = default)
    {
        var (channelId, messageTs) = ParseSlackUrl(slackUrl);

        using var client = httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BotToken);

        var msgs    = await GetMessagesAsync(client, channelId, messageTs, ct);
        var replies = await GetRepliesAsync(client, channelId, messageTs, ct);

        var files = new List<SlackFile>();
        foreach (var msg in msgs.Concat(replies))
        {
            foreach (var fileRef in msg.Files)
            {
                if (string.IsNullOrEmpty(fileRef.UrlPrivateDownload)) continue;
                var bytes = await DownloadFileAsync(client, fileRef.UrlPrivateDownload, ct);
                files.Add(new SlackFile(fileRef.Name, fileRef.MimeType, bytes));
            }
        }

        var text = BuildText(msgs, replies);
        return new SlackExtractResult(text, files);
    }

    /// <summary>
    /// Slack URL 파싱: https://{ws}.slack.com/archives/{channelId}/p{ts}
    /// p1742152800000000 → 1742152800.000000
    /// </summary>
    private static (string ChannelId, string MessageTs) ParseSlackUrl(string slackUrl)
    {
        var uri      = new Uri(slackUrl);
        var parts    = uri.AbsolutePath.TrimStart('/').Split('/');
        // /archives/{channelId}/p{ts}
        var channelId = parts[^2];
        var tsRaw     = parts[^1].TrimStart('p');
        // 마지막 6자리 앞에 '.' 삽입: "1742152800000000" → "1742152800.000000"
        var messageTs = tsRaw.Length > 6
            ? tsRaw[..^6] + "." + tsRaw[^6..]
            : tsRaw;
        return (channelId, messageTs);
    }

    private async Task<List<SlackMessage>> GetMessagesAsync(HttpClient client, string channelId, string messageTs, CancellationToken ct)
    {
        var url = $"https://slack.com/api/conversations.history?channel={channelId}&latest={messageTs}&inclusive=true&limit=1";
        var res = await client.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        if (!root.GetProperty("ok").GetBoolean())
        {
            var errCode = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "unknown";
            throw new InvalidOperationException($"Slack API error: {errCode}");
        }

        return root.GetProperty("messages").EnumerateArray().Select(ParseMessage).ToList();
    }

    private async Task<List<SlackMessage>> GetRepliesAsync(HttpClient client, string channelId, string messageTs, CancellationToken ct)
    {
        var url = $"https://slack.com/api/conversations.replies?channel={channelId}&ts={messageTs}";
        var res = await client.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        if (!root.GetProperty("ok").GetBoolean())
            return [];

        // 첫 번째 메시지는 원본 — skip
        return root.GetProperty("messages").EnumerateArray().Skip(1).Select(ParseMessage).ToList();
    }

    private static SlackMessage ParseMessage(JsonElement el)
    {
        var text  = el.TryGetProperty("text",  out var t) ? t.GetString() ?? "" : "";
        var user  = el.TryGetProperty("user",  out var u) ? u.GetString() ?? "" : "";
        var files = new List<SlackFileRef>();

        if (el.TryGetProperty("files", out var filesArr))
        {
            foreach (var f in filesArr.EnumerateArray())
            {
                files.Add(new SlackFileRef(
                    f.TryGetProperty("name",                 out var n)   ? n.GetString()   ?? "file"                    : "file",
                    f.TryGetProperty("mimetype",             out var m)   ? m.GetString()   ?? "application/octet-stream" : "application/octet-stream",
                    f.TryGetProperty("url_private_download", out var url) ? url.GetString() ?? ""                         : ""
                ));
            }
        }

        return new SlackMessage(user, text, files);
    }

    private static async Task<byte[]> DownloadFileAsync(HttpClient client, string url, CancellationToken ct)
    {
        var res = await client.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    private static string BuildText(List<SlackMessage> msgs, List<SlackMessage> replies)
    {
        var sb = new StringBuilder();
        foreach (var msg in msgs)
            sb.AppendLine(msg.Text);

        if (replies.Count > 0)
        {
            sb.AppendLine("\n--- 스레드 답글 ---");
            foreach (var reply in replies)
                sb.AppendLine($"> {reply.Text}");
        }

        return sb.ToString().Trim();
    }

    private record SlackMessage(string User, string Text, List<SlackFileRef> Files);
    private record SlackFileRef(string Name, string MimeType, string UrlPrivateDownload);
}
