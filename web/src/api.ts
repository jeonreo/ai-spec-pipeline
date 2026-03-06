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
  allOutputs: Record<string, string>,
  onChunk: (accumulated: string) => void,
): Promise<StreamResult> {
  const res = await fetch(`/api/run/stream/${profile}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ inputText, allOutputs }),
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

// ── Jira Integration ──────────────────────────────────────────────────────

export interface JiraProject  { key: string; name: string }
export interface JiraIssueType { id: string;  name: string }
export interface CreateJiraResult { key: string; url: string }

export interface JiraStatus {
  configured: boolean
  defaultProjectKey: string
  defaultIssueTypeName: string
}

export async function fetchJiraStatus(): Promise<JiraStatus> {
  const res = await fetch('/api/jira/status')
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)
  return res.json()
}

export async function fetchJiraProjects(): Promise<JiraProject[]> {
  const res = await fetch('/api/jira/projects')
  if (!res.ok) {
    const data = await res.json().catch(() => ({}))
    throw new Error(data.error ?? `서버 오류: ${res.status}`)
  }
  return res.json()
}

export async function fetchJiraIssueTypes(projectKey: string): Promise<JiraIssueType[]> {
  const res = await fetch(`/api/jira/issuetypes/${projectKey}`)
  if (!res.ok) {
    const data = await res.json().catch(() => ({}))
    throw new Error(data.error ?? `서버 오류: ${res.status}`)
  }
  return res.json()
}

export async function createJiraTicket(payload: {
  projectKey: string
  summary: string
  description: Record<string, string>
  acceptanceCriteria: string[]
  issueTypeId?: string
  issueTypeName?: string
  specContent?: string
}): Promise<CreateJiraResult> {
  const res = await fetch('/api/jira/create', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  const data = await res.json().catch(() => ({}))
  if (!res.ok) throw new Error(data.error ?? `서버 오류: ${res.status}`)
  return data
}

// ─────────────────────────────────────────────────────────────────────────────

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

// ── Settings ──────────────────────────────────────────────────────────────────

export interface PipelineSettings {
  stageModels: Record<string, string>
}

export async function fetchSettings(): Promise<PipelineSettings> {
  const res = await fetch('/api/settings')
  if (!res.ok) throw new Error()
  return res.json()
}

export async function saveSettings(settings: PipelineSettings): Promise<PipelineSettings> {
  const res = await fetch('/api/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  })
  if (!res.ok) throw new Error()
  return res.json()
}
