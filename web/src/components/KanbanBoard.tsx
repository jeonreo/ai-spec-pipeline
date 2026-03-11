import { useState } from 'react'
import { Tab, RunState } from '../App'
import CardDetailDrawer from './CardDetailDrawer'

// ── Types ───────────────────────────────────────────────────────
type BoardCol = 'queue' | 'running' | 'review' | 'done' | 'failed'

interface StageMeta {
  label: string
  icon: string
  tags: string[]
  priority: 'Critical' | 'High' | 'Normal'
  agent: string
}

const STAGE_META: Record<Tab, StageMeta> = {
  intake: { label: 'Intake Agent',  icon: '📥', tags: ['requirements', 'analysis'],   priority: 'Critical', agent: 'intake-agent' },
  spec:   { label: 'Spec Agent',    icon: '📋', tags: ['specification', 'planning'],  priority: 'Critical', agent: 'spec-agent'   },
  jira:   { label: 'Jira Agent',    icon: '🎯', tags: ['jira', 'ticket'],             priority: 'High',     agent: 'jira-agent'   },
  qa:     { label: 'QA Agent',      icon: '🧪', tags: ['testing', 'qa'],              priority: 'High',     agent: 'qa-agent'     },
  design: { label: 'Design Agent',  icon: '🎨', tags: ['design', 'handoff'],          priority: 'High',     agent: 'design-agent' },
}

const COLUMNS: { id: BoardCol; label: string }[] = [
  { id: 'queue',   label: 'To Do'       },
  { id: 'running', label: 'In Progress' },
  { id: 'review',  label: 'Review'      },
  { id: 'done',    label: 'Done'        },
  { id: 'failed',  label: 'Failed'      },
]

function getBoardCol(tab: Tab, state: RunState, decisionsConfirmed: boolean): BoardCol {
  if (state === 'running') return 'running'
  if (state === 'failed')  return 'failed'
  if (state === 'done') {
    if (tab === 'intake' && !decisionsConfirmed) return 'review'
    return 'done'
  }
  return 'queue'
}

// ── Task Card ────────────────────────────────────────────────────
interface CardProps {
  tab: Tab
  runState: RunState
  elapsed: number | null
  warning: string
  stale: boolean
  specDone: boolean
  onRun: () => void
  onOpen: () => void
  decisionsConfirmed: boolean
}

function TaskCard({ tab, runState, elapsed, warning, stale, specDone, onRun, onOpen, decisionsConfirmed }: CardProps) {
  const meta      = STAGE_META[tab]
  const isDone    = runState === 'done'
  const isRunning = runState === 'running'
  const isFailed  = runState === 'failed'
  const canRun    = !isRunning && (tab === 'intake' || tab === 'spec' || specDone)
  const col       = getBoardCol(tab, runState, decisionsConfirmed)

  return (
    <div
      className={`task-card task-card--${col}`}
      onClick={isDone || isRunning ? onOpen : undefined}
      style={{ cursor: isDone || isRunning ? 'pointer' : 'default' }}
    >
      <div className="task-card-top">
        <div className="task-card-title-row">
          <span className="task-card-icon">{meta.icon}</span>
          <span className="task-card-title">{meta.label}</span>
          <span className={`priority-badge priority-badge--${meta.priority.toLowerCase()}`}>
            {meta.priority}
          </span>
        </div>
        <div className="task-card-tags">
          {meta.tags.map(t => <span key={t} className="task-tag">{t}</span>)}
          {col === 'review' && tab === 'intake' && (
            <span className="task-tag task-tag--review">결정사항 필요</span>
          )}
          {stale && isDone && <span className="task-tag task-tag--stale">stale</span>}
          {warning && <span className="task-tag task-tag--warn" title={warning}>⚠ warn</span>}
        </div>
      </div>

      <div className="task-card-footer">
        <div className="task-card-agent">
          <span className={`agent-dot${isDone ? ' agent-dot--active' : ''}`} />
          <span className="agent-name">{meta.agent}</span>
          {elapsed !== null && <span className="agent-elapsed">{elapsed.toFixed(1)}s</span>}
          {isRunning && <span className="running-indicator"><span /><span /><span /></span>}
        </div>
        <div className="task-card-actions" onClick={e => e.stopPropagation()}>
          {(isDone || isRunning) && (
            <button className="btn-card-open" onClick={onOpen} title="상세 보기">
              ↗
            </button>
          )}
          <button
            className={`btn-card-run${isRunning ? ' btn-card-run--running' : isFailed ? ' btn-card-run--failed' : isDone ? ' btn-card-run--done' : ''}`}
            onClick={onRun}
            disabled={!canRun}
          >
            {isRunning ? '···' : isFailed ? '↺' : isDone ? '↺' : 'Run'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Kanban Board ─────────────────────────────────────────────────
interface BoardProps {
  outputs: Record<Tab, string>
  runStates: Record<Tab, RunState>
  stale: Record<Tab, boolean>
  elapsed: Record<Tab, number | null>
  warnings: Record<Tab, string>
  specDone: boolean
  decisionsConfirmed: boolean
  decisions: string
  jiraProjectKey: string
  jiraIssueTypeName: string
  onRun: (tab: Tab) => void
  onRunParallel: () => void
  onOutputChange: (tab: Tab, val: string) => void
  onDecisionsChange: (v: string) => void
  onConfirmAndRun: () => void
  onSkipAndRun: () => void
}

const ALL_STAGES: Tab[] = ['intake', 'spec', 'jira', 'qa', 'design']

export default function KanbanBoard({
  outputs, runStates, stale, elapsed, warnings,
  specDone, decisionsConfirmed, decisions, jiraProjectKey, jiraIssueTypeName,
  onRun, onRunParallel, onOutputChange, onDecisionsChange, onConfirmAndRun, onSkipAndRun,
}: BoardProps) {
  const [drawerTab, setDrawerTab] = useState<Tab | null>(null)

  const doneCount    = ALL_STAGES.filter(t => runStates[t] === 'done').length
  const runningCount = ALL_STAGES.filter(t => runStates[t] === 'running').length
  const colStages    = (col: BoardCol) =>
    ALL_STAGES.filter(t => getBoardCol(t, runStates[t], decisionsConfirmed) === col)

  return (
    <div className="kanban-board">
      {/* Sub-header */}
      <div className="board-subheader">
        <div className="board-stats">
          <span className="board-stat"><span className="stat-label">Total</span><span className="stat-value">{ALL_STAGES.length}</span></span>
          <span className="board-stat-sep" />
          <span className="board-stat"><span className="stat-label">Active</span><span className="stat-value stat-value--active">{runningCount}</span></span>
          <span className="board-stat-sep" />
          <span className="board-stat"><span className="stat-label">Done</span><span className="stat-value stat-value--done">{doneCount}</span></span>
          <span className="board-stat-sep" />
          <span className="board-stat"><span className="stat-label">Rate</span><span className="stat-value stat-value--rate">{Math.round(doneCount / ALL_STAGES.length * 100)}%</span></span>
        </div>
        <button
          className="btn-run-all"
          onClick={onRunParallel}
          disabled={runningCount > 0 || !specDone}
        >
          {runningCount > 0 ? '실행 중...' : '▶ 전체 실행'}
        </button>
      </div>

      {/* Columns */}
      <div className="board-columns">
        {COLUMNS.map(col => {
          const stages = colStages(col.id)
          return (
            <div key={col.id} className={`board-col board-col--${col.id}`}>
              <div className="board-col-header">
                <span className="board-col-dot" />
                <span className="board-col-label">{col.label}</span>
                <span className="board-col-count">{stages.length}</span>
              </div>
              <div className="board-col-body">
                {stages.length === 0 && <div className="board-col-empty">No tasks</div>}
                {stages.map(tab => (
                  <TaskCard
                    key={tab}
                    tab={tab}
                    runState={runStates[tab]}
                    elapsed={elapsed[tab]}
                    warning={warnings[tab]}
                    stale={stale[tab]}
                    specDone={specDone}
                    decisionsConfirmed={decisionsConfirmed}
                    onRun={() => onRun(tab)}
                    onOpen={() => setDrawerTab(tab)}
                  />
                ))}
              </div>
            </div>
          )
        })}
      </div>

      {/* Detail drawer */}
      {drawerTab && (
        <CardDetailDrawer
          tab={drawerTab}
          runState={runStates[drawerTab]}
          output={outputs[drawerTab]}
          specOutput={outputs.spec}
          elapsed={elapsed[drawerTab]}
          warning={warnings[drawerTab]}
          specDone={specDone}
          decisions={decisions}
          decisionsConfirmed={decisionsConfirmed}
          jiraProjectKey={jiraProjectKey}
          jiraIssueTypeName={jiraIssueTypeName}
          onOutputChange={val => onOutputChange(drawerTab, val)}
          onClose={() => setDrawerTab(null)}
          onRun={() => onRun(drawerTab)}
          onDecisionsChange={onDecisionsChange}
          onConfirmAndRun={onConfirmAndRun}
          onSkipAndRun={onSkipAndRun}
        />
      )}
    </div>
  )
}
