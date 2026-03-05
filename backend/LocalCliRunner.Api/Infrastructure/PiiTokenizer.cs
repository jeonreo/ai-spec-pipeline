using System.Text.RegularExpressions;

namespace LocalCliRunner.Api.Infrastructure;

/// <summary>
/// Claude CLI에 전송 전 PII를 토큰으로 치환하고, 응답 후 원래 값으로 복원한다.
/// </summary>
public class PiiTokenizer
{
    // 순서 중요: 더 구체적인 패턴이 먼저
    private static readonly (string Type, Regex Pattern)[] Patterns =
    [
        ("EMAIL", new Regex(
            @"[\w.+\-]+@[\w\-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)),

        ("PHONE", new Regex(
            @"(\+?82[\s\-]?|0)1[0-9][\s\-]?\d{3,4}[\s\-]?\d{4}" +  // 한국 휴대폰
            @"|\+?\d{1,3}[\s\-]\(?\d{2,4}\)?[\s\-]\d{3,4}[\s\-]\d{4}" + // 국제전화
            @"|\d{2,3}-\d{3,4}-\d{4}",                                 // 일반 유선
            RegexOptions.Compiled)),

        ("NAME", new Regex(
            @"(?<=(?:이름|담당자|고객명|작성자|성명|신청자|대표자)\s*[:：]\s*)[\p{IsCJKUnifiedIdeographs}\uAC00-\uD7A3\w]{2,6}",
            RegexOptions.Compiled)),

        ("ADDRESS", new Regex(
            @"(?<=(?:주소|거주지|소재지|배송지)\s*[:：]\s*)[^\n]{5,80}",
            RegexOptions.Compiled)),
    ];

    public (string Tokenized, Dictionary<string, string> Map) Tokenize(string input)
    {
        var map      = new Dictionary<string, string>(StringComparer.Ordinal);
        var counters = new Dictionary<string, int>();
        var result   = input;

        foreach (var (type, pattern) in Patterns)
        {
            result = pattern.Replace(result, match =>
            {
                var value = match.Value.Trim();
                if (string.IsNullOrWhiteSpace(value)) return match.Value;

                // 같은 값이 이미 토큰화됐으면 재사용
                var existing = map.FirstOrDefault(kv => kv.Value == value).Key;
                if (existing is not null) return existing;

                counters.TryGetValue(type, out var n);
                var token = $"[PII_{type}_{n + 1}]";
                counters[type] = n + 1;
                map[token] = value;
                return token;
            });
        }

        return (result, map);
    }

    public string Detokenize(string output, Dictionary<string, string> map)
    {
        foreach (var (token, value) in map)
            output = output.Replace(token, value, StringComparison.Ordinal);
        return output;
    }
}
