import { useState, useEffect } from 'react'
import { flushSync } from 'react-dom'
import { streamStage, fetchPolicy } from './api'
import SourcePanel from './components/SourcePanel'
import SpecPanel from './components/SpecPanel'
import OutputPanel from './components/OutputPanel'
import HistoryPanel from './components/HistoryPanel'

export type Tab = 'intake' | 'spec' | 'jira' | 'qa' | 'design'
export type RunState = 'idle' | 'running' | 'done' | 'failed'

const TABS: Tab[] = ['intake', 'spec', 'jira', 'qa', 'design']

const DOWNSTREAM: Record<Tab, Tab[]> = {
  intake: ['spec'],
  spec:   ['jira', 'qa', 'design'],
  jira:   [],
  qa:     [],
  design: [],
}

const EMPTY_OUTPUTS: Record<Tab, string> = { intake: '', spec: '', jira: '', qa: '', design: '' }
const EMPTY_STATES:  Record<Tab, RunState> = { intake: 'idle', spec: 'idle', jira: 'idle', qa: 'idle', design: 'idle' }
const EMPTY_ELAPSED: Record<Tab, number | null> = { intake: null, spec: null, jira: null, qa: null, design: null }
const EMPTY_WARNINGS: Record<Tab, string> = { intake: '', spec: '', jira: '', qa: '', design: '' }
const EMPTY_STALE:   Record<Tab, boolean> = { intake: false, spec: false, jira: false, qa: false, design: false }

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
  spec:   (ctx) => ctx.outputs.intake,
  jira:   (ctx) => ctx.outputs.spec,
  qa:     (ctx) => ctx.outputs.spec,
  design: (ctx) => extractSpecSections(ctx.outputs.spec, ['## 기능 요약', '## UI 구성']),
}

interface Context {
  input: string
  outputs: Record<Tab, string>
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
  const [stale, setStale]       = useState<Record<Tab, boolean>>(EMPTY_STALE)
  const [policy, setPolicy]     = useState<string | null>(null)
  const [policyOpen, setPolicyOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)

  // Auto-save session to localStorage
  useEffect(() => {
    const data = {
      input,
      outputs,
      runStates: sanitizeRunStates(runStates),
      elapsed,
      warnings,
    }
    localStorage.setItem(SESSION_KEY, JSON.stringify(data))
  }, [input, outputs, runStates, elapsed, warnings])

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

  async function handleRun(tab: Tab) {
    const inputText = STAGE_INPUT[tab]({ input, outputs })
    if (!inputText.trim()) {
      setStageError(tab, '입력 내용이 없습니다.')
      setTimeout(() => setStageError(tab, ''), 3000)
      return
    }

    setStageState(tab, 'running')
    setStageError(tab, '')
    setWarnings(prev => ({ ...prev, [tab]: '' }))
    setElapsed(prev => ({ ...prev, [tab]: null }))
    setStale(prev => ({ ...prev, [tab]: false }))

    const startedAt = Date.now()

    try {
      const result = await streamStage(tab, inputText, outputs, (accumulated) => {
        flushSync(() => {
          setOutputs(prev => ({ ...prev, [tab]: accumulated }))
        })
      })

      const elapsedSec = (Date.now() - startedAt) / 1000
      setOutputs(prev => ({ ...prev, [tab]: result.output }))
      setElapsed(prev => ({ ...prev, [tab]: elapsedSec }))
      setStageState(tab, 'done')
      if (result.warning) setWarnings(prev => ({ ...prev, [tab]: result.warning! }))

      // Mark downstream stages stale
      setStale(prev => {
        const next = { ...prev, [tab]: false }
        for (const ds of DOWNSTREAM[tab]) next[ds] = true
        return next
      })
    } catch (e) {
      setStageError(tab, e instanceof Error ? e.message : '오류 발생')
      setStageState(tab, 'failed')
    }
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
    setStale({ ...EMPTY_STALE })
    localStorage.removeItem(SESSION_KEY)
  }

  function handleRestore(inputText: string, restoredOutputs: Partial<Record<Tab, string>>) {
    setInput(inputText)
    setOutputs(prev => ({ ...prev, ...restoredOutputs }))
    setRunStates(prev => {
      const next = { ...prev }
      for (const tab of Object.keys(restoredOutputs) as Tab[]) next[tab] = 'done'
      return next
    })
    setElapsed({ ...EMPTY_ELAPSED })
    setStale({ ...EMPTY_STALE })
  }

  function handleOutputChange(tab: Tab, val: string) {
    setOutputs(prev => ({ ...prev, [tab]: val }))
    // Mark downstream stale when user manually edits
    setStale(prev => {
      const next = { ...prev }
      for (const ds of DOWNSTREAM[tab]) next[ds] = true
      return next
    })
  }

  const anyError = Object.values(errors).find(Boolean)

  return (
    <div id="root">
      <header className="app-header">
        <div className="app-header-left">
          <span className="app-title">AI Spec Pipeline</span>
          <span className="app-pipeline-flow">Source → Decision Spec → Outputs</span>
        </div>
        <div className="header-actions">
          {anyError && <span className="run-error">{anyError}</span>}
          <button className="btn-policy" onClick={() => setHistoryOpen(true)}>히스토리</button>
          <button className="btn-policy" onClick={handlePolicyOpen}>비즈니스 정책</button>
          <button className="btn-reset" onClick={handleReset}>새 사이클</button>
        </div>
      </header>

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

      <main className="dashboard">
        <SourcePanel
          input={input}
          onInputChange={setInput}
          onRun={handleRun}
          runStates={runStates}
          stale={stale}
        />
        <SpecPanel
          content={outputs.spec}
          onChange={val => handleOutputChange('spec', val)}
          runState={runStates.spec}
          elapsed={elapsed.spec}
          warning={warnings.spec}
          stale={stale.spec}
          onRun={() => handleRun('spec')}
        />
        <OutputPanel
          outputs={outputs}
          runStates={runStates}
          stale={stale}
          elapsed={elapsed}
          warnings={warnings}
          specDone={runStates.spec === 'done'}
          onRun={handleRun}
          onRunParallel={handleRunParallel}
          onOutputChange={handleOutputChange}
        />
      </main>
    </div>
  )
}
