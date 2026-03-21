using System.Text;
using System.Text.RegularExpressions;

namespace LocalCliRunner.Api.Infrastructure;

public record RepoFile(string Path, string Content);

/// <summary>
/// GitHub Code Search API를 사용해 관련 파일을 찾고 LLM 컨텍스트로 조립한다.
/// GitHub:Token이 없으면 빈 목록을 반환해 graceful degradation.
/// </summary>
public class RepoSearchService(GitHubService gitHub, ILogger<RepoSearchService> logger)
{
    private const int MaxFileChars  = 8_000;  // 파일 하나당 최대
    private const int MaxTotalChars = 60_000; // 전체 컨텍스트 예산 (기본값)

    public async Task<List<RepoFile>> SearchAsync(string repoUrl, string query, int maxFiles = 10, int maxTotalChars = MaxTotalChars, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
            return [];

        logger.LogInformation("코드 검색: repo={Repo}, query={Query}", repoUrl, query[..Math.Min(80, query.Length)]);

        try
        {
            var items      = await gitHub.SearchCodeAsync(repoUrl, query, maxFiles, ct);
            var files      = new List<RepoFile>();
            var totalChars = 0;

            foreach (var item in items)
            {
                if (totalChars >= maxTotalChars || ct.IsCancellationRequested) break;
                try
                {
                    var file    = await gitHub.GetFileContentAsync(repoUrl, item.Path, ct: ct);
                    var content = file.Content.Length > MaxFileChars
                        ? file.Content[..MaxFileChars] + "\n... (truncated)"
                        : file.Content;
                    files.Add(new RepoFile(item.Path, content));
                    totalChars += content.Length;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "파일 조회 실패: {Path}", item.Path);
                }
            }

            logger.LogInformation("코드 검색 완료: {Count}개 파일, {Chars}자", files.Count, totalChars);
            return files;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "코드 검색 중 오류");
            return [];
        }
    }

    /// <summary>파일 목록을 LLM이 읽을 수 있는 마크다운 블록으로 조립한다.</summary>
    public static string BuildContext(List<RepoFile> files)
    {
        if (files.Count == 0) return "";

        var sb = new StringBuilder();
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f.Path).TrimStart('.') switch
            {
                "cs"   => "csharp",
                "ts"   => "typescript",
                "tsx"  => "typescript",
                "js"   => "javascript",
                "jsx"  => "javascript",
                "json" => "json",
                "md"   => "markdown",
                "py"   => "python",
                var e  => e,
            };
            sb.AppendLine($"### {f.Path}");
            sb.AppendLine($"```{ext}");
            sb.AppendLine(f.Content);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>스펙 또는 분석 텍스트에서 코드 검색에 적합한 키워드를 추출한다.</summary>
    public static string ExtractSearchKeywords(string text)
    {
        // H1 heading 우선
        var h1Match = Regex.Match(text, @"^#\s+(.+)$", RegexOptions.Multiline);
        if (h1Match.Success) return h1Match.Groups[1].Value.Trim();

        // 첫 번째 H2
        var h2Match = Regex.Match(text, @"^##\s+(.+)$", RegexOptions.Multiline);
        if (h2Match.Success) return h2Match.Groups[1].Value.Trim();

        // fallback: 첫 200자에서 마크다운 기호 제거
        var clean = Regex.Replace(text, @"^#{1,6}\s+", "", RegexOptions.Multiline)
                         .Replace('\n', ' ')
                         .Trim();
        return clean[..Math.Min(200, clean.Length)];
    }
}
