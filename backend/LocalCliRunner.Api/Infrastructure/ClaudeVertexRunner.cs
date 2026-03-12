using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace LocalCliRunner.Api.Infrastructure;

/// <summary>
/// Vertex AI Claude API를 사용하는 ICliRunner 구현체.
/// 인증은 Application Default Credentials (ADC) 를 사용한다.
/// 로컬: gcloud auth application-default login
/// Cloud Run: 서비스 계정 자동 인증
/// </summary>
public class ClaudeVertexRunner(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    ILogger<ClaudeVertexRunner> logger) : ICliRunner
{
    private string ProjectId    => config["Vertex:ProjectId"]    ?? throw new InvalidOperationException("Vertex:ProjectId is not configured.");
    private string Location     => config["Vertex:Location"]     ?? "global";
    private string DefaultModel => config["Vertex:DefaultModel"] ?? "claude-sonnet-4-6";
    private int MaxTokens       => int.TryParse(config["Vertex:MaxTokens"], out var n) ? n : 8192;

    public async Task<CliResult> RunAsync(string promptContent, string workspacePath, string? model = null, CancellationToken ct = default)
    {
        var modelId = model ?? DefaultModel;
        logger.LogInformation("Vertex AI Claude call: project={Project}, location={Location}, model={Model}", ProjectId, Location, modelId);

        try
        {
            var token   = await GetTokenAsync();
            var url     = BuildUrl(modelId, stream: false);
            var body    = BuildBody(promptContent);

            using var client   = httpFactory.CreateClient();
            using var request  = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, ct);
            var responseBody   = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Vertex AI Claude call failed: {Status} {Body}", response.StatusCode, responseBody);
                return new CliResult(1, string.Empty, $"HTTP {response.StatusCode}: {responseBody}");
            }

            using var doc  = JsonDocument.Parse(responseBody);
            var root       = doc.RootElement;
            var text       = ExtractText(root);
            var usage      = ExtractUsage(root);

            logger.LogInformation("Vertex AI Claude call succeeded, output length={Length}", text.Length);
            return new CliResult(0, text, string.Empty, usage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vertex AI Claude call failed");
            return new CliResult(1, string.Empty, ex.Message);
        }
    }

    public async Task<TokenUsage?> StreamAsync(string promptContent, string workspacePath, Func<string, Task> onChunk, string? model = null, CancellationToken ct = default)
    {
        var modelId = model ?? DefaultModel;
        logger.LogInformation("Vertex AI Claude stream: project={Project}, location={Location}, model={Model}", ProjectId, Location, modelId);

        var token  = await GetTokenAsync();
        var url    = BuildUrl(modelId, stream: true);
        var body   = BuildBody(promptContent);

        using var client  = httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"HTTP {response.StatusCode}: {errBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        int inputTokens  = 0;
        int outputTokens = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (!line.StartsWith("data: ")) continue;

            var json = line[6..].Trim();
            if (json == "[DONE]") break;
            if (string.IsNullOrEmpty(json)) continue;

            try
            {
                using var doc  = JsonDocument.Parse(json);
                var root       = doc.RootElement;
                var type       = root.GetProperty("type").GetString();

                switch (type)
                {
                    case "content_block_delta":
                        if (root.TryGetProperty("delta", out var delta) &&
                            delta.TryGetProperty("type", out var deltaType) &&
                            deltaType.GetString() == "text_delta" &&
                            delta.TryGetProperty("text", out var textProp))
                        {
                            var chunk = textProp.GetString();
                            if (!string.IsNullOrEmpty(chunk))
                                await onChunk(chunk);
                        }
                        break;

                    case "message_start":
                        if (root.TryGetProperty("message", out var msg) &&
                            msg.TryGetProperty("usage", out var startUsage) &&
                            startUsage.TryGetProperty("input_tokens", out var inputProp))
                        {
                            inputTokens = inputProp.GetInt32();
                        }
                        break;

                    case "message_delta":
                        if (root.TryGetProperty("usage", out var deltaUsage) &&
                            deltaUsage.TryGetProperty("output_tokens", out var outputProp))
                        {
                            outputTokens = outputProp.GetInt32();
                        }
                        break;
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse SSE line: {Line}", json);
            }
        }

        return new TokenUsage(inputTokens, outputTokens);
    }

    private async Task<string> GetTokenAsync()
    {
        var credential = await GoogleCredential.GetApplicationDefaultAsync();
        credential = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform");
        return await ((ITokenAccess)credential).GetAccessTokenForRequestAsync();
    }

    private string BuildUrl(string modelId, bool stream)
    {
        var action = stream ? "streamRawPredict" : "rawPredict";
        // global 엔드포인트 사용 시 host는 aiplatform.googleapis.com
        var host = Location == "global"
            ? "aiplatform.googleapis.com"
            : $"{Location}-aiplatform.googleapis.com";
        return $"https://{host}/v1/projects/{ProjectId}/locations/{Location}/publishers/anthropic/models/{modelId}:{action}";
    }

    private string BuildBody(string promptContent)
    {
        var payload = new
        {
            anthropic_version = "vertex-2023-10-16",
            messages = new[] { new { role = "user", content = promptContent } },
            max_tokens = MaxTokens,
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content)) return string.Empty;

        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                block.TryGetProperty("text", out var textProp))
            {
                sb.Append(textProp.GetString());
            }
        }
        return sb.ToString();
    }

    private static TokenUsage? ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage)) return null;

        var input  = usage.TryGetProperty("input_tokens",  out var i) ? i.GetInt32() : 0;
        var output = usage.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : 0;
        return new TokenUsage(input, output);
    }
}
