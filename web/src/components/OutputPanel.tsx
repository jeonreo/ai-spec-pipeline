import { useState } from 'react'
import type { ReactNode } from 'react'
import { Tab, RunState } from '../App'
import DesignPackageView from './DesignPackageView'
import JiraView from './JiraView'

interface CardProps {
  title: string
  icon: string
  description: string
  runState: RunState
  stale: boolean
  elapsed: number | null
  warning: string
  specDone: boolean
  defaultCollapsed?: boolean
  onRun: () => void
  children: ReactNode
}

function OutputCard({
  title,
  icon,
  description,
  runState,
  stale,
  elapsed,
  warning,
  specDone,
  defaultCollapsed = false,
  onRun,
  children,
}: CardProps) {
  const isDone = runState === 'done'
  const isRunning = runState === 'running'
  const isFailed = runState === 'failed'
  const [collapsed, setCollapsed] = useState(defaultCollapsed)

  const showBody = (isDone || isRunning) && !collapsed
  const canRun = specDone && !isRunning

  return (
    <div className={`output-card${isDone ? ' output-card--done' : ''}${isFailed ? ' output-card--failed' : ''}`}>
      <div className="output-card-header">
        <span className="output-card-icon">{icon}</span>
        <div className="output-card-meta">
          <span className="output-card-title">{title}</span>
          <span className="output-card-desc">{description}</span>
        </div>
        <div className="output-card-actions">
          {elapsed !== null && <span className="elapsed-badge">{elapsed.toFixed(1)}s</span>}
          {isDone && stale && <span className="stage-stale-badge">stale</span>}
          {warning && <span className="warning-dot" title={warning}>!</span>}
          <button
            className={`btn-generate${isRunning ? ' btn-generate--running' : isDone ? ' btn-generate--done' : ''}`}
            onClick={onRun}
            disabled={!canRun}
            title={!specDone ? 'Generate the decision spec first.' : undefined}
          >
            {isRunning ? 'Running...' : isDone ? 'Regenerate' : 'Generate'}
          </button>
          {(isDone || isRunning) && (
            <button
              className="btn-collapse"
              onClick={() => setCollapsed(value => !value)}
              title={collapsed ? 'Expand' : 'Collapse'}
            >
              {collapsed ? '+' : '-'}
            </button>
          )}
        </div>
      </div>
      {showBody && <div className="output-card-body">{children}</div>}
    </div>
  )
}

const PLANNED_OUTPUTS = [
  { title: 'FE Implementation', icon: 'FE', desc: 'Frontend code from spec' },
  { title: 'BE Implementation', icon: 'BE', desc: 'Backend API from spec' },
  { title: 'QA Automation', icon: 'AT', desc: 'Automated test scripts' },
]

interface Props {
  outputs: Record<Tab, string>
  runStates: Record<Tab, RunState>
  stale: Record<Tab, boolean>
  elapsed: Record<Tab, number | null>
  warnings: Record<Tab, string>
  specDone: boolean
  onRun: (tab: Tab) => void
  onRunParallel: () => void
  onOutputChange: (tab: Tab, val: string) => void
}

export default function OutputPanel({
  outputs,
  runStates,
  stale,
  elapsed,
  warnings,
  specDone,
  onRun,
  onRunParallel,
  onOutputChange,
}: Props) {
  const parallelRunning = (['jira', 'qa', 'design'] as Tab[]).some(tab => runStates[tab] === 'running')

  return (
    <div className="output-panel">
      <div className="panel-header">
        <span className="panel-title">Outputs</span>
        <button
          className="btn-parallel"
          onClick={onRunParallel}
          disabled={parallelRunning || !specDone}
          title="Generate Jira, QA, and Design outputs in parallel."
        >
          {parallelRunning ? 'Running...' : 'Generate All'}
        </button>
      </div>

      <div className="output-cards">
        <OutputCard
          title="Jira Ticket"
          icon="JR"
          description="Create a Jira issue from the decision spec"
          runState={runStates.jira}
          stale={stale.jira}
          elapsed={elapsed.jira}
          warning={warnings.jira}
          specDone={specDone}
          onRun={() => onRun('jira')}
        >
          <JiraView
            content={outputs.jira}
            onChange={value => onOutputChange('jira', value)}
            specContent={outputs.spec}
          />
        </OutputCard>

        <OutputCard
          title="QA Test Cases"
          icon="QA"
          description="Create test scenarios from acceptance criteria"
          defaultCollapsed
          runState={runStates.qa}
          stale={stale.qa}
          elapsed={elapsed.qa}
          warning={warnings.qa}
          specDone={specDone}
          onRun={() => onRun('qa')}
        >
          <textarea
            className="output-textarea"
            value={outputs.qa}
            onChange={event => onOutputChange('qa', event.target.value)}
          />
        </OutputCard>

        <OutputCard
          title="Design Package"
          icon="DS"
          description="Structured handoff package and optional Clover preview"
          defaultCollapsed
          runState={runStates.design}
          stale={stale.design}
          elapsed={elapsed.design}
          warning={warnings.design}
          specDone={specDone}
          onRun={() => onRun('design')}
        >
          <DesignPackageView
            content={outputs.design}
            onChange={value => onOutputChange('design', value)}
          />
        </OutputCard>

        <div className="planned-section">
          <span className="planned-section-title">Next Outputs</span>
          {PLANNED_OUTPUTS.map(item => (
            <div key={item.title} className="planned-card">
              <span className="planned-icon">{item.icon}</span>
              <div className="planned-info">
                <span className="planned-name">{item.title}</span>
                <span className="planned-desc">{item.desc}</span>
              </div>
              <span className="badge-planned">planned</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
