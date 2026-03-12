export interface TokenUsage {
  inputTokens: number
  outputTokens: number
}

export interface StreamResult {
  output: string
  warning?: string
  tokens?: TokenUsage
}

const STREAM_UPDATE_INTERVAL_MS = 75

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
  let flushTimer: number | null = null

  const flushAccumulated = () => {
    if (flushTimer !== null) {
      window.clearTimeout(flushTimer)
      flushTimer = null
    }
    onChunk(accumulated)
  }

  const scheduleFlush = () => {
    if (flushTimer !== null) return
    flushTimer = window.setTimeout(() => {
      flushTimer = null
      onChunk(accumulated)
    }, STREAM_UPDATE_INTERVAL_MS)
  }

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    sseBuffer += decoder.decode(value, { stream: true })
    const events = sseBuffer.split('\n\n')
    sseBuffer = events.pop() ?? ''

    for (const event of events) {
      if (!event.startsWith('data: ')) continue
      const data = JSON.parse(event.slice(6))
      if (data.done) {
        flushAccumulated()
        return { output: data.output, warning: data.warning ?? undefined, tokens: data.tokens ?? undefined }
      }
      if (data.chunk) {
        accumulated += data.chunk
        scheduleFlush()
      }
    }
  }
  flushAccumulated()
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

export async function deleteHistoryItem(id: string): Promise<void> {
  const res = await fetch(`/api/history/${id}`, { method: 'DELETE' })
  if (!res.ok) throw new Error(`?쒕쾭 ?ㅻ쪟: ${res.status}`)
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

// ── Project Knowledge ─────────────────────────────────────────────────────────

export async function consolidateKnowledge(
  knowledge: string,
  onChunk: (accumulated: string) => void,
): Promise<string> {
  const res = await fetch('/api/knowledge/consolidate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ knowledge }),
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
      if (data.done) return data.output as string
      if (data.chunk) { accumulated += data.chunk; onChunk(accumulated) }
    }
  }
  return accumulated
}

// ── GitHub Integration ─────────────────────────────────────────────────────────

export interface GitHubRepoStatus {
  label: string
  url: string
  connected: boolean
  repoName?: string
  defaultBranch?: string
  error?: string
}

export interface CreatePrPayload {
  title: string
  patches: { repo?: string; path: string; content: string; comment?: string }[]
  specSummary?: string
  analysisSummary?: string
}

export interface PrResult {
  label: string
  prUrl?: string
  error?: string
}

export async function fetchGithubStatus(): Promise<GitHubRepoStatus[]> {
  const res = await fetch('/api/github/status')
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)
  return res.json()
}

export async function createPullRequest(payload: CreatePrPayload): Promise<{ results: PrResult[] }> {
  const res = await fetch('/api/github/pr', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  const data = await res.json().catch(() => ({}))
  if (!res.ok) throw new Error(data.error ?? `서버 오류: ${res.status}`)
  return data
}

// ── Settings ──────────────────────────────────────────────────────────────────

export interface GitHubSettings {
  frontendRepoUrl: string
  backendRepoUrl: string
}

export interface PipelineSettings {
  stageModels: Record<string, string>
  github: GitHubSettings
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
