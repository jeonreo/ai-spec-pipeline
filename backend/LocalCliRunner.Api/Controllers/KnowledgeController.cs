using System.Text;
using System.Text.Json;
using LocalCliRunner.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalCliRunner.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public class KnowledgeController(ICliRunner cliRunner) : ControllerBase
{
    private const string SystemPrompt = """
프로젝트 지식 항목들을 분석하여 다음 작업을 수행하세요:
1. 중복되거나 유사한 항목을 하나로 통합
2. 상충되는 결정은 더 구체적인 내용으로 업데이트
3. 아래 카테고리로 구조화 (해당 항목이 없으면 카테고리 생략)

출력 형식:
## 기술 결정
- ...

## 범위 및 제약
- ...

## 팀 컨벤션
- ...

## 기타
- ...

서문 없이 마크다운만 바로 출력하세요.

---
## 정리할 지식

""";

    // POST /api/knowledge/consolidate
    [HttpPost("consolidate")]
    public async Task Consolidate([FromBody] ConsolidateRequest req, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var prompt = SystemPrompt + req.Knowledge;
        var workDir = Path.Combine(Path.GetTempPath(), "ai-spec-knowledge");
        Directory.CreateDirectory(workDir);

        var sb = new StringBuilder();

        await cliRunner.StreamAsync(prompt, workDir, async chunk =>
        {
            sb.Append(chunk);
            var json = JsonSerializer.Serialize(new { chunk });
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }, ct: ct);

        var done = JsonSerializer.Serialize(new { done = true, output = sb.ToString().TrimEnd() });
        await Response.WriteAsync($"data: {done}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}

public record ConsolidateRequest(string Knowledge);
