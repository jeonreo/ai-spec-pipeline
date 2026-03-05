using System.Collections.Concurrent;
using LocalCliRunner.Api.Domain;

namespace LocalCliRunner.Api.Infrastructure;

/// <summary>
/// 인메모리 Job 레지스트리 (싱글턴). 서버 재시작 시 초기화됨.
/// </summary>
public class JobRegistry
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new();

    public void Register(Job job) => _jobs[job.Id] = job;

    public Job? Get(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job : null;

    public IEnumerable<Job> GetAll() =>
        _jobs.Values.OrderByDescending(j => j.CreatedAt);
}
