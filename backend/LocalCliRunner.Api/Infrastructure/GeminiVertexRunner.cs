using Google.Cloud.AIPlatform.V1;
using Google.Protobuf.Collections;

namespace LocalCliRunner.Api.Infrastructure;

/// <summary>
/// Vertex AI Gemini API를 사용하는 ICliRunner 구현체.
/// 인증은 Application Default Credentials (ADC) 를 사용한다.
/// 로컬: gcloud auth application-default login
/// Cloud Run: 서비스 계정 자동 인증
/// </summary>
public class GeminiVertexRunner(IConfiguration config, ILogger<GeminiVertexRunner> logger) : ICliRunner
{
    private string ProjectId    => config["Vertex:ProjectId"]    ?? throw new InvalidOperationException("Vertex:ProjectId is not configured.");
    private string Location     => config["Vertex:Location"]     ?? "us-central1";
    private string DefaultModel => config["Vertex:DefaultModel"] ?? "gemini-2.0-flash-001";

    public async Task<CliResult> RunAsync(string promptContent, string workspacePath, string? model = null, CancellationToken ct = default)
    {
        var modelId = model ?? DefaultModel;
        logger.LogInformation("Vertex AI call: project={Project}, location={Location}, model={Model}", ProjectId, Location, modelId);

        try
        {
            var client   = await BuildClientAsync();
            var response = await client.GenerateContentAsync(BuildRequest(modelId, promptContent), ct);
            var text     = ExtractText(response);
            TokenUsage? usage = response.UsageMetadata is null ? null
                : new TokenUsage(response.UsageMetadata.PromptTokenCount, response.UsageMetadata.CandidatesTokenCount);

            logger.LogInformation("Vertex AI call succeeded, output length={Length}", text.Length);
            return new CliResult(0, text, string.Empty, usage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vertex AI call failed");
            return new CliResult(1, string.Empty, ex.Message);
        }
    }

    public async Task<TokenUsage?> StreamAsync(string promptContent, string workspacePath, Func<string, Task> onChunk, string? model = null, CancellationToken ct = default)
    {
        var modelId = model ?? DefaultModel;
        logger.LogInformation("Vertex AI stream: project={Project}, location={Location}, model={Model}", ProjectId, Location, modelId);

        var client         = await BuildClientAsync();
        var streamCall     = client.StreamGenerateContent(BuildRequest(modelId, promptContent));
        var responseStream = streamCall.GetResponseStream();

        GenerateContentResponse.Types.UsageMetadata? lastUsage = null;

        await foreach (var response in responseStream.WithCancellation(ct))
        {
            var chunk = ExtractText(response);
            if (!string.IsNullOrEmpty(chunk))
                await onChunk(chunk);
            if (response.UsageMetadata is not null)
                lastUsage = response.UsageMetadata;
        }

        return lastUsage is null ? null
            : new TokenUsage(lastUsage.PromptTokenCount, lastUsage.CandidatesTokenCount);
    }

    private async Task<PredictionServiceClient> BuildClientAsync()
    {
        var endpoint = $"{Location}-aiplatform.googleapis.com";
        return await new PredictionServiceClientBuilder { Endpoint = endpoint }.BuildAsync();
    }

    private GenerateContentRequest BuildRequest(string modelId, string promptContent)
    {
        var modelName = $"projects/{ProjectId}/locations/{Location}/publishers/google/models/{modelId}";

        return new GenerateContentRequest
        {
            Model    = modelName,
            Contents = { new Content { Role = "user", Parts = { new Part { Text = promptContent } } } },
            GenerationConfig = new GenerationConfig
            {
                Temperature     = 0.2f,
                MaxOutputTokens = 8192,
            },
        };
    }

    private static string ExtractText(GenerateContentResponse response)
    {
        var parts = response.Candidates
            .SelectMany(c => c.Content?.Parts ?? (RepeatedField<Part>)[])
            .Select(p => p.Text)
            .Where(t => !string.IsNullOrEmpty(t));

        return string.Concat(parts);
    }
}
