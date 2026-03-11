import { useState, useEffect, startTransition } from 'react'
import { streamStage, fetchPolicy } from './api'
import SourcePanel from './components/SourcePanel'
import KanbanBoard from './components/KanbanBoard'
import HistoryPanel from './components/HistoryPanel'
import SettingsModal from './components/SettingsModal'

export type Tab = 'intake' | 'spec' | 'jira' | 'qa' | 'design'
export type RunState = 'idle' | 'running' | 'done' | 'failed'

const TABS: Tab[] = ['intake', 'spec', 'jira', 'qa', 'design']

const EMPTY_OUTPUTS: Record<Tab, string> = { intake: '', spec: '', jira: '', qa: '', design: '' }
const EMPTY_STATES:  Record<Tab, RunState> = { intake: 'idle', spec: 'idle', jira: 'idle', qa: 'idle', design: 'idle' }
const EMPTY_ELAPSED: Record<Tab, number | null> = { intake: null, spec: null, jira: null, qa: null, design: null }
const EMPTY_WARNINGS: Record<Tab, string> = { intake: '', spec: '', jira: '', qa: '', design: '' }
const EMPTY_SIGNATURES: Record<Tab, string> = { intake: '', spec: '', jira: '', qa: '', design: '' }

const SESSION_KEY = 'ai-spec-pipeline-session'

function loadSession() {
  try {
    const raw = localStorage.getItem(SESSION_KEY)
    return raw ? JSON.parse(raw) : null
  } catch { return null }
}

const _saved = loadSession()

function sanitizeRunStates(rs: Record<Tab, RunState>): Record<Tab, RunState> {
  const next = { ...rs }
  for (const tab of TABS) if (next[tab] === 'running') next[tab] = 'idle'
  return next
}

function normalizeStageInput(text: string): string {
  return text.replace(/\r\n/g, '\n').trim()
}

function hashString(text: string): string {
  let hash = 2166136261

  for (let i = 0; i < text.length; i += 1) {
    hash ^= text.charCodeAt(i)
    hash = Math.imul(hash, 16777619)
  }

  return (hash >>> 0).toString(16).padStart(8, '0')
}

function extractSpecSections(spec: string, headings: string[]): string {
  const lines = spec.split('\n')
  const result: string[] = []
  let include = false
  for (const line of lines) {
    if (line.startsWith('## ')) include = headings.some(h => line.startsWith(h))
    if (include) result.push(line)
  }
  return result.join('\n')
}

const STAGE_INPUT: Record<Tab, (ctx: Context) => string> = {
  intake: (ctx) => ctx.input,
  spec:   (ctx) => {
    const base = ctx.outputs.intake
    if (!ctx.decisions.trim()) return base
    return `${base}\n\n---\n## 결정사항\n\n${ctx.decisions}`
  },
  jira:   (ctx) => ctx.outputs.spec,
  qa:     (ctx) => ctx.outputs.spec,
  design: (ctx) => extractSpecSections(ctx.outputs.spec, ['## 기능 요약', '## UI 구성']),
}

interface Context {
  input: string
  outputs: Record<Tab, string>
  decisions: string
}

function buildStageInputSignatures(ctx: Context): Record<Tab, string> {
  return {
    intake: hashString(normalizeStageInput(STAGE_INPUT.intake(ctx))),
    spec:   hashString(normalizeStageInput(STAGE_INPUT.spec(ctx))),
    jira:   hashString(normalizeStageInput(STAGE_INPUT.jira(ctx))),
    qa:     hashString(normalizeStageInput(STAGE_INPUT.qa(ctx))),
    design: hashString(normalizeStageInput(STAGE_INPUT.design(ctx))),
  }
}

function buildStaleFlags(
  runStates: Record<Tab, RunState>,
  currentInputSignatures: Record<Tab, string>,
  completedInputSignatures: Record<Tab, string>,
): Record<Tab, boolean> {
  const hasCompletedSignature = (tab: Tab) => completedInputSignatures[tab] !== ''

  return {
    intake: runStates.intake === 'done' && hasCompletedSignature('intake') && currentInputSignatures.intake !== completedInputSignatures.intake,
    spec:   runStates.spec === 'done' && hasCompletedSignature('spec') && currentInputSignatures.spec !== completedInputSignatures.spec,
    jira:   runStates.jira === 'done' && hasCompletedSignature('jira') && currentInputSignatures.jira !== completedInputSignatures.jira,
    qa:     runStates.qa === 'done' && hasCompletedSignature('qa') && currentInputSignatures.qa !== completedInputSignatures.qa,
    design: runStates.design === 'done' && hasCompletedSignature('design') && currentInputSignatures.design !== completedInputSignatures.design,
  }
}

export default function App() {
  const [input, setInput]       = useState<string>(_saved?.input ?? '')
  const [outputs, setOutputs]   = useState<Record<Tab, string>>({ ...EMPTY_OUTPUTS, ..._saved?.outputs })
  const [runStates, setRunStates] = useState<Record<Tab, RunState>>(
    _saved?.runStates ? sanitizeRunStates({ ...EMPTY_STATES, ..._saved.runStates }) : EMPTY_STATES
  )
  const [errors, setErrors]     = useState<Record<Tab, string>>({ ...EMPTY_WARNINGS })
  const [warnings, setWarnings] = useState<Record<Tab, string>>({ ...EMPTY_WARNINGS, ..._saved?.warnings })
  const [elapsed, setElapsed]   = useState<Record<Tab, number | null>>({ ...EMPTY_ELAPSED, ..._saved?.elapsed })
  const [completedInputSignatures, setCompletedInputSignatures] = useState<Record<Tab, string>>(
    { ...EMPTY_SIGNATURES, ..._saved?.completedInputSignatures }
  )
  const [decisions, setDecisions] = useState<string>(_saved?.decisions ?? '')
  const [decisionsConfirmed, setDecisionsConfirmed] = useState<boolean>(_saved?.decisionsConfirmed ?? false)
  const [jiraProjectKey, setJiraProjectKey] = useState<string>(_saved?.jiraProjectKey ?? '')
  const [jiraIssueTypeName, setJiraIssueTypeName] = useState<string>(_saved?.jiraIssueTypeName ?? '')
  const [projectKnowledge, setProjectKnowledge] = useState<string>(_saved?.projectKnowledge ?? '')
  const [policy, setPolicy]     = useState<string | null>(null)
  const [policyOpen, setPolicyOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)

  const stageContext: Context = { input, outputs, decisions }
  const currentInputSignatures = buildStageInputSignatures(stageContext)
  const stale = buildStaleFlags(runStates, currentInputSignatures, completedInputSignatures)

  // Auto-save session to localStorage
  useEffect(() => {
    const data = {
      input,
      outputs,
      runStates: sanitizeRunStates(runStates),
      elapsed,
      warnings,
      completedInputSignatures,
      decisions,
      decisionsConfirmed,
      jiraProjectKey,
      jiraIssueTypeName,
      projectKnowledge,
    }
    localStorage.setItem(SESSION_KEY, JSON.stringify(data))
  }, [input, outputs, runStates, elapsed, warnings, completedInputSignatures, decisions, decisionsConfirmed, jiraProjectKey, jiraIssueTypeName, projectKnowledge])

  async function handlePolicyOpen() {
    if (!policy) {
      const content = await fetchPolicy()
      setPolicy(content)
    }
    setPolicyOpen(true)
  }

  function setStageState(tab: Tab, state: RunState) {
    setRunStates(prev => ({ ...prev, [tab]: state }))
  }

  function setStageError(tab: Tab, msg: string) {
    setErrors(prev => ({ ...prev, [tab]: msg }))
  }

  async function handleRun(tab: Tab, inputOverride?: string): Promise<string | null> {
    const inputText = inputOverride ?? STAGE_INPUT[tab](stageContext)
    const inputSignature = currentInputSignatures[tab]
    if (!inputText.trim()) {
      setStageError(tab, '입력 내용이 없습니다.')
      setTimeout(() => setStageError(tab, ''), 3000)
      return null
    }

    setStageState(tab, 'running')
    setStageError(tab, '')
    setWarnings(prev => ({ ...prev, [tab]: '' }))
    setElapsed(prev => ({ ...prev, [tab]: null }))

    const startedAt = Date.now()

    try {
      const result = await streamStage(tab, inputText, outputs, (accumulated) => {
        startTransition(() => {
          setOutputs(prev => ({ ...prev, [tab]: accumulated }))
        })
      })

      const elapsedSec = (Date.now() - startedAt) / 1000
      setOutputs(prev => ({ ...prev, [tab]: result.output }))
      setElapsed(prev => ({ ...prev, [tab]: elapsedSec }))
      setStageState(tab, 'done')
      setCompletedInputSignatures(prev => ({ ...prev, [tab]: inputSignature }))
      if (result.warning) setWarnings(prev => ({ ...prev, [tab]: result.warning! }))
      return result.output
    } catch (e) {
      setStageError(tab, e instanceof Error ? e.message : '오류 발생')
      setStageState(tab, 'failed')
      return null
    }
  }

  async function handleConfirmAndAutoRun() {
    setDecisionsConfirmed(true)
    const specInput = STAGE_INPUT.spec(stageContext)
    const specOutput = await handleRun('spec', specInput)
    if (!specOutput) return
    await Promise.all([
      handleRun('jira',   specOutput),
      handleRun('qa',     specOutput),
      handleRun('design', extractSpecSections(specOutput, ['## 기능 요약', '## UI 구성'])),
    ])
  }

  async function handleSkipAndAutoRun() {
    setDecisionsConfirmed(true)
    const specInput = STAGE_INPUT.spec({ ...stageContext, decisions: '' })
    const specOutput = await handleRun('spec', specInput)
    if (!specOutput) return
    await Promise.all([
      handleRun('jira',   specOutput),
      handleRun('qa',     specOutput),
      handleRun('design', extractSpecSections(specOutput, ['## 기능 요약', '## UI 구성'])),
    ])
  }

  function handleRunParallel() {
    Promise.all((['jira', 'qa', 'design'] as Tab[]).map(tab => handleRun(tab)))
  }

  function handleReset() {
    setInput('')
    setOutputs({ ...EMPTY_OUTPUTS })
    setRunStates({ ...EMPTY_STATES })
    setErrors({ ...EMPTY_WARNINGS })
    setWarnings({ ...EMPTY_WARNINGS })
    setElapsed({ ...EMPTY_ELAPSED })
    setCompletedInputSignatures({ ...EMPTY_SIGNATURES })
    setDecisions('')
    setDecisionsConfirmed(false)
    localStorage.removeItem(SESSION_KEY)
  }

  function handleRestore(inputText: string, restoredOutputs: Partial<Record<Tab, string>>) {
    const nextOutputs = { ...outputs, ...restoredOutputs }
    const restoredContext = { input: inputText, outputs: nextOutputs }
    const restoredSignatures = buildStageInputSignatures(restoredContext)

    setInput(inputText)
    setOutputs(nextOutputs)
    setRunStates(prev => {
      const next = { ...prev }
      for (const tab of Object.keys(restoredOutputs) as Tab[]) next[tab] = 'done'
      return next
    })
    setElapsed({ ...EMPTY_ELAPSED })
    setCompletedInputSignatures(prev => {
      const next = { ...prev }
      for (const tab of Object.keys(restoredOutputs) as Tab[]) next[tab] = restoredSignatures[tab]
      return next
    })
  }

  function handleOutputChange(tab: Tab, val: string) {
    setOutputs(prev => ({ ...prev, [tab]: val }))
    setWarnings(prev => ({ ...prev, [tab]: '' }))
  }

  const anyError = Object.values(errors).find(Boolean)

  return (
    <div id="root">
      <header className="app-header">
        <div className="app-header-left">
          <span className="app-title">AI Spec Pipeline</span>
          <span className="live-badge"><span className="live-dot" />Live</span>
        </div>
        <div className="header-actions">
          {anyError && <span className="run-error">{anyError}</span>}
          <button className="btn-header" onClick={() => setHistoryOpen(true)}>히스토리</button>
          <button className="btn-header" onClick={() => setSettingsOpen(true)}>모델 설정</button>
          <button className="btn-header" onClick={handlePolicyOpen}>비즈니스 정책</button>
          <button className="btn-new-cycle" onClick={handleReset}>+ New Cycle</button>
        </div>
      </header>

      {settingsOpen && (
        <SettingsModal onClose={() => setSettingsOpen(false)} />
      )}

      {historyOpen && (
        <HistoryPanel
          onRestore={handleRestore}
          onClose={() => setHistoryOpen(false)}
        />
      )}

      {policyOpen && (
        <div className="policy-overlay" onClick={() => setPolicyOpen(false)}>
          <div className="policy-modal" onClick={e => e.stopPropagation()}>
            <div className="policy-modal-header">
              <span>비즈니스 정책</span>
              <button onClick={() => setPolicyOpen(false)}>✕</button>
            </div>
            <pre className="policy-modal-body">{policy}</pre>
          </div>
        </div>
      )}

      <main className="board-layout">
        {/* Left sidebar: source input */}
        <div className="board-sidebar">
          <SourcePanel
            input={input}
            onInputChange={setInput}
            onRun={handleRun}
            runStates={runStates}
            stale={stale}
            jiraProjectKey={jiraProjectKey}
            jiraIssueTypeName={jiraIssueTypeName}
            onJiraConfigChange={(key, type) => { setJiraProjectKey(key); setJiraIssueTypeName(type) }}
            projectKnowledge={projectKnowledge}
            onProjectKnowledgeChange={setProjectKnowledge}
            decisions={decisions}
            intakeOutput={outputs.intake}
          />
        </div>

        {/* Main kanban board */}
        <KanbanBoard
          outputs={outputs}
          runStates={runStates}
          stale={stale}
          elapsed={elapsed}
          warnings={warnings}
          specDone={runStates.spec === 'done'}
          decisionsConfirmed={decisionsConfirmed}
          decisions={decisions}
          jiraProjectKey={jiraProjectKey}
          jiraIssueTypeName={jiraIssueTypeName}
          onRun={handleRun}
          onRunParallel={handleRunParallel}
          onOutputChange={handleOutputChange}
          onDecisionsChange={setDecisions}
          onConfirmAndRun={handleConfirmAndAutoRun}
          onSkipAndRun={handleSkipAndAutoRun}
        />
      </main>
    </div>
  )
}
