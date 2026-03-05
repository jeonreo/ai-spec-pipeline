export type JobStatus = 'queued' | 'running' | 'done' | 'failed'

export interface JobResult {
  jobId: string
  status: JobStatus
  outputContent?: string
  error?: string
}

export interface StreamResult {
  output: string
  warning?: string
}

export async function streamStage(
  profile: string,
  inputText: string,
  onChunk: (accumulated: string) => void,
): Promise<StreamResult> {
  const res = await fetch(`/api/run/stream/${profile}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ inputText }),
  })
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)

  const reader = res.body!.getReader()
  const decoder = new TextDecoder()
  let sseBuffer = ''
  let accumulated = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    sseBuffer += decoder.decode(value, { stream: true })
    const events = sseBuffer.split('\n\n')
    sseBuffer = events.pop() ?? ''

    for (const event of events) {
      if (!event.startsWith('data: ')) continue
      const data = JSON.parse(event.slice(6))
      if (data.done) return { output: data.output, warning: data.warning ?? undefined }
      if (data.chunk) {
        accumulated += data.chunk
        onChunk(accumulated)
      }
    }
  }
  return { output: accumulated }
}

export interface HistoryItem {
  id: string
  inputPreview: string
  stages: string[]
}

export interface HistoryPage {
  total: number
  page: number
  pageSize: number
  items: HistoryItem[]
}

export interface HistoryDetail {
  id: string
  inputText: string
  outputs: Record<string, string>
}

export async function fetchHistory(page: number, pageSize: number, date?: string): Promise<HistoryPage> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (date) params.set('date', date)
  const res = await fetch(`/api/history?${params}`)
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)
  return res.json()
}

export async function fetchHistoryDetail(id: string): Promise<HistoryDetail> {
  const res = await fetch(`/api/history/${id}`)
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)
  return res.json()
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
