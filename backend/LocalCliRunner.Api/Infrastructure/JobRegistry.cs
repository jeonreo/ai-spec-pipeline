using System.Collections.Concurrent;
using LocalCliRunner.Api.Domain;

namespace LocalCliRunner.Api.Infrastructure;

/// <summary>
/// 인메모리 Job 레지스트리 (싱글턴). 서버 재시작 시 초기화됨.
/// 최대 MaxJobs개 유지 — 초과 시 가장 오래된 항목부터 제거.
/// </summary>
public class JobRegistry
{
    private const int MaxJobs = 200;

    private readonly ConcurrentDictionary<string, Job> _jobs = new();

    public void Register(Job job)
    {
        _jobs[job.Id] = job;

        if (_jobs.Count > MaxJobs)
        {
            var toRemove = _jobs.Values
                .OrderBy(j => j.CreatedAt)
                .Take(_jobs.Count - MaxJobs)
                .Select(j => j.Id)
                .ToList();

            foreach (var id in toRemove)
                _jobs.TryRemove(id, out _);
        }
    }

    public Job? Get(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job : null;

    public IEnumerable<Job> GetAll() =>
        _jobs.Values.OrderByDescending(j => j.CreatedAt);

    /// <summary>
    /// 완료된 Job의 OutputContent를 메모리에서 해제한다.
    /// 파일은 WorkspacePath에 이미 저장되어 있으므로 데이터 손실 없음.
    /// </summary>
    public void ClearContent(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
            job.OutputContent = null;
    }
}
