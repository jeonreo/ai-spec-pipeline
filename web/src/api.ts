export type JobStatus = 'queued' | 'running' | 'done' | 'failed'

export interface JobResult {
  jobId: string
  status: JobStatus
  outputContent?: string
  error?: string
}

export async function fetchPolicy(): Promise<string> {
  const res = await fetch('/api/policy')
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)
  const data = await res.json()
  return data.content
}

export async function runStage(profile: string, inputText: string): Promise<{ jobId: string }> {
  const res = await fetch(`/api/run/${profile}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ inputText }),
  })
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)
  return res.json()
}

export async function pollJob(jobId: string): Promise<JobResult> {
  const res = await fetch(`/api/run/${jobId}`)
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)
  return res.json()
}

export async function pollUntilDone(
  jobId: string,
  onStatus: (result: JobResult) => void,
  intervalMs = 500,
): Promise<JobResult> {
  return new Promise((resolve, reject) => {
    const tick = async () => {
      try {
        const result = await pollJob(jobId)
        onStatus(result)
        if (result.status === 'done' || result.status === 'failed') {
          resolve(result)
        } else {
          setTimeout(tick, intervalMs)
        }
      } catch (e) {
        reject(e)
      }
    }
    tick()
  })
}
