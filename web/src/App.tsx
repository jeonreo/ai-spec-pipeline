import { useState } from 'react'
import { flushSync } from 'react-dom'
import { streamStage, fetchPolicy } from './api'
import InputPanel from './components/InputPanel'
import OutputTabs from './components/OutputTabs'
import HistoryPanel from './components/HistoryPanel'

export type Tab = 'intake' | 'spec' | 'jira' | 'qa' | 'design'
export type RunState = 'idle' | 'running' | 'done' | 'failed'

const TABS: Tab[] = ['intake', 'spec', 'jira', 'qa', 'design']

const STAGE_INPUT: Record<Tab, (ctx: Context) => string> = {
  intake: (ctx) => ctx.input,
  spec:   (ctx) => ctx.outputs.intake,
  jira:   (ctx) => ctx.outputs.spec,
  qa:     (ctx) => ctx.outputs.spec,
  design: (ctx) => ctx.outputs.spec,
}

interface Context {
  input: string
  outputs: Record<Tab, string>
}

export default function App() {
  const [input, setInput]     = useState('')
  const [outputs, setOutputs] = useState<Record<Tab, string>>({ intake: '', spec: '', jira: '', qa: '', design: '' })
  const [runStates, setRunStates] = useState<Record<Tab, RunState>>({ intake: 'idle', spec: 'idle', jira: 'idle', qa: 'idle', design: 'idle' })
  const [errors, setErrors]   = useState<Record<Tab, string>>({ intake: '', spec: '', jira: '', qa: '', design: '' })
  const [warnings, setWarnings] = useState<Record<Tab, string>>({ intake: '', spec: '', jira: '', qa: '', design: '' })
  const [activeTab, setActiveTab] = useState<Tab>('intake')
  const [elapsed, setElapsed] = useState<Record<Tab, number | null>>({ intake: null, spec: null, jira: null, qa: null, design: null })
  const [policy, setPolicy] = useState<string | null>(null)
  const [policyOpen, setPolicyOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)

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
    setActiveTab(tab)

    const startedAt = Date.now()

    try {
      const result = await streamStage(tab, inputText, (accumulated) => {
        flushSync(() => {
          setOutputs(prev => ({ ...prev, [tab]: accumulated }))
        })
      })

      const elapsedSec = (Date.now() - startedAt) / 1000
      setOutputs(prev => ({ ...prev, [tab]: result.output }))
      setElapsed(prev => ({ ...prev, [tab]: elapsedSec }))
      setStageState(tab, 'done')
      if (result.warning) setWarnings(prev => ({ ...prev, [tab]: result.warning! }))
    } catch (e) {
      setStageError(tab, e instanceof Error ? e.message : '오류 발생')
      setStageState(tab, 'failed')
    }
  }

  function handleReset() {
    setInput('')
    setOutputs({ intake: '', spec: '', jira: '', qa: '', design: '' })
    setRunStates({ intake: 'idle', spec: 'idle', jira: 'idle', qa: 'idle', design: 'idle' })
    setErrors({ intake: '', spec: '', jira: '', qa: '', design: '' })
    setWarnings({ intake: '', spec: '', jira: '', qa: '', design: '' })
    setElapsed({ intake: null, spec: null, jira: null, qa: null, design: null })
    setActiveTab('intake')
  }

  function handleRestore(inputText: string, restoredOutputs: Partial<Record<Tab, string>>) {
    setInput(inputText)
    setOutputs(prev => ({ ...prev, ...restoredOutputs }))
    setRunStates(prev => {
      const next = { ...prev }
      for (const tab of Object.keys(restoredOutputs) as Tab[]) next[tab] = 'done'
      return next
    })
    setElapsed({ intake: null, spec: null, jira: null, qa: null, design: null })
    const firstTab = TABS.find(t => restoredOutputs[t])
    if (firstTab) setActiveTab(firstTab)
  }

  const anyError = Object.values(errors).find(Boolean)

  return (
    <div id="root" style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
      <header className="app-header">
        AI Spec Pipeline
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

      <main className="app-main">
        <InputPanel
          input={input}
          onInputChange={setInput}
          onRun={handleRun}
          runStates={runStates}
        />
        <OutputTabs
          outputs={outputs}
          activeTab={activeTab}
          onTabChange={setActiveTab}
          onOutputChange={(tab, val) => setOutputs(prev => ({ ...prev, [tab]: val }))}
          elapsed={elapsed}
          warnings={warnings}
        />
      </main>
    </div>
  )
}
