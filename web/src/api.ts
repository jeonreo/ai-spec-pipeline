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
      if (data.error) {
        throw new Error(data.error)
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

export async function streamStageWithFiles(
  profile: string,
  inputText: string,
  allOutputs: Record<string, string>,
  files: File[],
  onChunk: (accumulated: string) => void,
): Promise<StreamResult> {
  const formData = new FormData()
  formData.append('inputText', inputText)
  formData.append('allOutputsJson', JSON.stringify(allOutputs))
  files.forEach(f => formData.append('files', f))

  const res = await fetch(`/api/run/stream-files/${profile}`, {
    method: 'POST',
    body: formData,
  })
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)

  const reader = res.body!.getReader()
  const decoder = new TextDecoder()
  let sseBuffer = ''
  let accumulated = ''
  let flushTimer: number | null = null

  const flushAccumulated = () => {
    if (flushTimer !== null) { window.clearTimeout(flushTimer); flushTimer = null }
    onChunk(accumulated)
  }
  const scheduleFlush = () => {
    if (flushTimer !== null) return
    flushTimer = window.setTimeout(() => { flushTimer = null; onChunk(accumulated) }, STREAM_UPDATE_INTERVAL_MS)
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
      if (data.done) { flushAccumulated(); return { output: data.output, warning: data.warning ?? undefined, tokens: data.tokens ?? undefined } }
      if (data.error) throw new Error(data.error)
      if (data.chunk) { accumulated += data.chunk; scheduleFlush() }
    }
  }
  flushAccumulated()
  return { output: accumulated }
}

// ── Slack Integration ─────────────────────────────────────────────────────────

export interface SlackFile {
  name: string
  mimeType: string
  base64: string
}

export interface SlackExtractResult {
  text: string
  files: SlackFile[]
}

export async function fetchSlackStatus(): Promise<{ configured: boolean }> {
  const res = await fetch('/api/slack/status')
  if (!res.ok) throw new Error(`서버 오류: ${res.status}`)
  return res.json()
}

export async function extractFromSlack(url: string): Promise<SlackExtractResult> {
  const res = await fetch('/api/slack/extract', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ url }),
  })
  const data = await res.json().catch(() => ({}))
  if (!res.ok) throw new Error(data.error ?? `서버 오류: ${res.status}`)
  return data
}

export async function attachSlackFilesToJira(issueKey: string, files: SlackFile[]): Promise<void> {
  const formData = new FormData()
  for (const f of files) {
    const bytes = Uint8Array.from(atob(f.base64), c => c.charCodeAt(0))
    const blob  = new Blob([bytes], { type: f.mimeType })
    formData.append('files', blob, f.name)
  }
  const res = await fetch(`/api/jira/${issueKey}/attach`, {
    method: 'POST',
    body: formData,
  })
  if (!res.ok) {
    const data = await res.json().catch(() => ({}))
    throw new Error(data.error ?? `파일 첨부 실패 (${res.status})`)
  }
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

export interface WorkflowListItem {
  id: string
  source: string
  status: string
  currentStage: string
  createdAt: string
  updatedAt: string
  requestUserName: string
  requestPreview: string
  jiraIssueKey?: string
  origin: WorkflowOriginRef
}

export interface WorkflowStageState {
  name: string
  status: string
  threadTs: string
  workerMessageTs: string
  reviewerMessageTs: string
  approvalMessageTs: string
  lastInput: string
  outputPreview: string
  outputFile: string
  reviewerOutputFile: string
  reviewerPreview: string
  reviewerDecision: string
  reviewerSummary: string
  lastFeedback: string
  lastError: string
  startedAt?: string
  completedAt?: string
}

export interface WorkflowSlackRef {
  userId: string
  userName: string
  channelId: string
  rootMessageTs: string
  stageThreadTs: Record<string, string>
}

export interface WorkflowOriginRef {
  triggerType: string
  channelId: string
  messageTs: string
  threadTs: string
  eventId: string
}

export interface WorkflowJiraDraft {
  projectKey: string
  issueTypeId: string
  issueTypeName: string
}

export interface WorkflowJiraResult {
  issueKey: string
  issueUrl: string
  createdAt: string
}

export interface WorkflowState {
  id: string
  source: string
  workspaceId: string
  workspacePath: string
  requestText: string
  requestUserId: string
  requestUserName: string
  status: string
  currentStage: string
  pendingFeedbackStage: string
  lastError: string
  createdAt: string
  updatedAt: string
  slack: WorkflowSlackRef
  origin: WorkflowOriginRef
  jiraDraft: WorkflowJiraDraft
  jiraResult?: WorkflowJiraResult
  stages: Record<string, WorkflowStageState>
  outputFiles: Record<string, string>
}

export interface WorkflowDetail {
  workflow: WorkflowState
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

export async function fetchWorkflows(): Promise<{ items: WorkflowListItem[] }> {
  const res = await fetch('/api/workflows')
  if (!res.ok) throw new Error(`?쒕쾭 ?ㅻ쪟: ${res.status}`)
  return res.json()
}

export async function fetchWorkflowDetail(id: string): Promise<WorkflowDetail> {
  const res = await fetch(`/api/workflows/${id}`)
  if (!res.ok) throw new Error(`?쒕쾭 ?ㅻ쪟: ${res.status}`)
  return res.json()
}

export async function rerunWorkflowStage(id: string, stage?: string): Promise<void> {
  const res = await fetch(`/api/workflows/${id}/rerun`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ stage }),
  })
  const data = await res.json().catch(() => ({}))
  if (!res.ok) throw new Error(data.error ?? `?쒕쾭 ?ㅻ쪟: ${res.status}`)
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

export async function addJiraRemoteLink(issueKey: string, url: string, title: string): Promise<void> {
  const res = await fetch(`/api/jira/${issueKey}/remotelink`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ url, title }),
  })
  if (!res.ok) {
    const data = await res.json().catch(() => ({}))
    throw new Error(data.error ?? `Jira 링크 추가 실패 (${res.status})`)
  }
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

type PatchItem = { repo?: string; path: string; content: string; comment?: string }

export interface PushBranchPayload {
  title?: string
  patches: PatchItem[]
  specSummary?: string
  analysisSummary?: string
}

export interface PushBranchResult {
  label: string
  branchName?: string
  branchUrl?: string
  filesCommitted?: number
  error?: string
}

export interface CreatePrPayload {
  branchName: string
  title?: string
  repos?: string[]
  specSummary?: string
  analysisSummary?: string
  patches?: PatchItem[]
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

export async function pushBranch(payload: PushBranchPayload): Promise<{ results: PushBranchResult[] }> {
  const res = await fetch('/api/github/push', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  const data = await res.json().catch(() => ({}))
  if (!res.ok) throw new Error(data.error ?? `서버 오류: ${res.status}`)
  return data
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
  isVertex?: boolean
  codeBudgetKb?: number
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

export async function fetchOriginalFile(repo: string, path: string): Promise<string | null> {
  const params = new URLSearchParams({ repo, path })
  const res = await fetch(`/api/github/file-content?${params}`)
  if (!res.ok) return null
  const data = await res.json().catch(() => null)
  return data?.content ?? null
}

// ── Learn Agent ───────────────────────────────────────────────────────────────

export interface LearnPatch {
  stage: string
  skillPatch: string
}

export interface LearnApplyResult {
  applied: string[]
  errors: string[]
}

export async function applyLearnSuggestions(patches: LearnPatch[]): Promise<LearnApplyResult> {
  const res = await fetch('/api/learn/apply', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ patches }),
  })
  const data = await res.json().catch(() => ({}))
  if (!res.ok) throw new Error(data.error ?? `서버 오류: ${res.status}`)
  return data
}

export async function updatePolicy(decisions: string): Promise<void> {
  const res = await fetch('/api/policy/update', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ decisions }),
  })
  if (!res.ok) throw new Error(`정책 업데이트 실패: ${res.status}`)
}
