import { useState } from 'react'
import { runStage, pollUntilDone, JobResult } from './api'
import InputPanel from './components/InputPanel'
import OutputTabs from './components/OutputTabs'

export type Tab = 'intake' | 'spec' | 'jira' | 'qa' | 'design'
export type RunState = 'idle' | 'running' | 'done' | 'failed'

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
  const [activeTab, setActiveTab] = useState<Tab>('intake')
  const [elapsed, setElapsed] = useState<Record<Tab, number | null>>({ intake: null, spec: null, jira: null, qa: null, design: null })

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
    setElapsed(prev => ({ ...prev, [tab]: null }))
    setActiveTab(tab)

    const startedAt = Date.now()

    try {
      const { jobId } = await runStage(tab, inputText)

      const result: JobResult = await pollUntilDone(jobId, (r) => {
        // Could show intermediate status updates here
      })

      const elapsedSec = (Date.now() - startedAt) / 1000
      if (result.status === 'done') {
        setOutputs(prev => ({ ...prev, [tab]: result.outputContent ?? '' }))
        setElapsed(prev => ({ ...prev, [tab]: elapsedSec }))
        setStageState(tab, 'done')
      } else {
        setStageError(tab, result.error ?? '실행 실패')
        setStageState(tab, 'failed')
      }
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
    setElapsed({ intake: null, spec: null, jira: null, qa: null, design: null })
    setActiveTab('intake')
  }

  const anyError = Object.values(errors).find(Boolean)

  return (
    <div id="root" style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
      <header className="app-header">
        AI Spec Pipeline
        <div className="header-actions">
          {anyError && <span className="run-error">{anyError}</span>}
          <button className="btn-reset" onClick={handleReset}>새 사이클</button>
        </div>
      </header>
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
        />
      </main>
    </div>
  )
}
