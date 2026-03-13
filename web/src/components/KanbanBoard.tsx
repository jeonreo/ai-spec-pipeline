import { useState } from 'react'
import { Tab, RunState } from '../App'
import { TokenUsage, PushBranchResult, PrResult } from '../api'
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
  intake:          { label: 'Intake Agent',  icon: '📥', tags: ['requirements', 'analysis'],   priority: 'Critical', agent: 'intake-agent'  },
  spec:            { label: 'Spec Agent',    icon: '📋', tags: ['specification', 'planning'],  priority: 'Critical', agent: 'spec-agent'    },
  jira:            { label: 'Jira Agent',    icon: '🎯', tags: ['jira', 'ticket'],             priority: 'High',     agent: 'jira-agent'    },
  qa:              { label: 'QA Agent',      icon: '🧪', tags: ['testing', 'qa'],              priority: 'High',     agent: 'qa-agent'      },
  design:          { label: 'Design Agent',  icon: '🎨', tags: ['design', 'handoff'],          priority: 'High',     agent: 'design-agent'  },
  'code-analysis': { label: 'Code Agent',    icon: '🔍', tags: ['code', 'analysis'],           priority: 'High',     agent: 'code-agent'    },
  patch:           { label: 'Patch Agent',   icon: '🩹', tags: ['patch', 'code-change'],       priority: 'High',     agent: 'patch-agent'   },
}

const COLUMNS: { id: BoardCol; label: string }[] = [
  { id: 'queue',   label: 'To Do'       },
  { id: 'running', label: 'In Progress' },
  { id: 'review',  label: 'Review'      },
  { id: 'done',    label: 'Done'        },
  { id: 'failed',  label: 'Failed'      },
]

function getBoardCol(tab: Tab, state: RunState, decisionsConfirmed: boolean, specDone: boolean): BoardCol {
  if (state === 'running') return 'running'
  if (state === 'failed')  return 'failed'
  if (state === 'done') {
    if (tab === 'intake' && !decisionsConfirmed) return 'review'
    return 'done'
  }
  // code-analysis/patch는 spec이 완료되어야 queue에 표시
  if ((tab === 'code-analysis' || tab === 'patch') && !specDone) return 'queue'
  return 'queue'
}

// ── Task Card ────────────────────────────────────────────────────
interface CardProps {
  tab: Tab
  runState: RunState
  elapsed: number | null
  tokens: TokenUsage | null
  warning: string
  stale: boolean
  specDone: boolean
  codeAnalysisDone: boolean
  output: string
  onRun: () => void
  onOpen: () => void
  decisionsConfirmed: boolean
}

function TaskCard({ tab, runState, elapsed, tokens, warning, stale, specDone, codeAnalysisDone, output, onRun, onOpen, decisionsConfirmed }: CardProps) {
  const meta      = STAGE_META[tab]
  const isDone    = runState === 'done'
  const isRunning = runState === 'running'
  const isFailed  = runState === 'failed'
  const canRun = !isRunning && (
    tab === 'intake' ||
    tab === 'spec'   ||
    (specDone && (tab === 'jira' || tab === 'qa' || tab === 'design' || tab === 'code-analysis')) ||
    (specDone && codeAnalysisDone && tab === 'patch')
  )
  const col = getBoardCol(tab, runState, decisionsConfirmed, specDone)

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

      {(isDone || col === 'review') && output && (
        <div className="task-card-preview" onClick={onOpen}>
          {output.slice(0, 120).replace(/[#*`>\-]/g, '').trim()}{output.length > 120 ? '…' : ''}
        </div>
      )}

      <div className="task-card-footer">
        <div className="task-card-agent">
          <span className={`agent-dot${isDone ? ' agent-dot--active' : ''}`} />
          <span className="agent-name">{meta.agent}</span>
          {elapsed !== null && <span className="agent-elapsed">{elapsed.toFixed(1)}s</span>}
          {tokens !== null && <span className="agent-tokens" title={`input: ${tokens.inputTokens.toLocaleString()} / output: ${tokens.outputTokens.toLocaleString()}`}>{tokens.inputTokens.toLocaleString()}/{tokens.outputTokens.toLocaleString()}</span>}
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
  tokens: Record<Tab, TokenUsage | null>
  warnings: Record<Tab, string>
  specDone: boolean
  decisionsConfirmed: boolean
  decisions: string
  jiraProjectKey: string
  jiraIssueTypeName: string
  jiraIssueKey: string
  jiraLinkError: string
  pushResults: PushBranchResult[]
  pushCreating: boolean
  prResults: PrResult[]
  prCreating: boolean
  onRun: (tab: Tab) => void
  onRunParallel: () => void
  onOutputChange: (tab: Tab, val: string) => void
  onDecisionsChange: (v: string) => void
  onConfirmAndRun: () => void
  onSkipAndRun: () => void
  onPushBranch: () => void
  onCreatePr: () => void
  onJiraCreated?: (key: string) => void
}

const ALL_STAGES: Tab[] = ['intake', 'spec', 'jira', 'qa', 'design', 'code-analysis', 'patch']

export default function KanbanBoard({
  outputs, runStates, stale, elapsed, tokens, warnings,
  specDone, decisionsConfirmed, decisions, jiraProjectKey, jiraIssueTypeName,
  jiraIssueKey, jiraLinkError,
  pushResults, pushCreating, prResults, prCreating,
  onRun, onRunParallel, onOutputChange, onDecisionsChange, onConfirmAndRun, onSkipAndRun,
  onPushBranch, onCreatePr, onJiraCreated,
}: BoardProps) {
  const [drawerTab, setDrawerTab] = useState<Tab | null>(null)

  const doneCount    = ALL_STAGES.filter(t => runStates[t] === 'done').length
  const runningCount = ALL_STAGES.filter(t => runStates[t] === 'running').length
  const colStages    = (col: BoardCol) =>
    ALL_STAGES.filter(t => getBoardCol(t, runStates[t], decisionsConfirmed, specDone) === col)
  const patchDone    = runStates.patch === 'done'

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
                    tokens={tokens[tab]}
                    warning={warnings[tab]}
                    output={outputs[tab]}
                    stale={stale[tab]}
                    specDone={specDone}
                    codeAnalysisDone={runStates['code-analysis'] === 'done'}
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

      {/* PR Agent bar — patch 완료 시 표시 */}
      {patchDone && (
        <div className="pr-agent-bar">
          <div className="pr-agent-bar-left">
            <span className="pr-agent-icon">🚀</span>
            <span className="pr-agent-label">PR Agent</span>
            <span className="task-tag">github</span>
            <span className="task-tag">pr-draft</span>
            <span className="task-tag">FE + BE</span>
            {jiraIssueKey && !jiraLinkError && (pushResults.length > 0 || prResults.length > 0) && (
              <span className="task-tag task-tag--jira-linked">🎯 {jiraIssueKey} 연결됨</span>
            )}
            {jiraIssueKey && !jiraLinkError && pushResults.length === 0 && (
              <span className="task-tag task-tag--jira-pending">🎯 {jiraIssueKey}</span>
            )}
            {jiraLinkError && (
              <span className="task-tag task-tag--warn" title={jiraLinkError}>🎯 Jira 연결 실패</span>
            )}
          </div>
          <div className="pr-agent-bar-right">
            {prResults.length > 0 ? (
              /* Step 3: PR URLs */
              <div className="pr-results">
                {prResults.map(r => r.prUrl ? (
                  <a key={r.label} className="btn-pr-url" href={r.prUrl} target="_blank" rel="noreferrer">
                    {r.label.toUpperCase()} PR ↗
                  </a>
                ) : (
                  <span key={r.label} className="pr-result-error" title={r.error}>
                    {r.label.toUpperCase()} 실패
                  </span>
                ))}
              </div>
            ) : pushResults.length > 0 ? (
              /* Step 2: Branch links + Create PR button */
              <div className="pr-push-done">
                <div className="pr-branch-links">
                  {pushResults.map(r => r.branchUrl ? (
                    <a key={r.label} className="btn-branch-url" href={r.branchUrl} target="_blank" rel="noreferrer">
                      {r.label.toUpperCase()} 브랜치 ↗
                    </a>
                  ) : (
                    <span key={r.label} className="pr-result-error" title={r.error}>
                      {r.label.toUpperCase()} 실패
                    </span>
                  ))}
                </div>
                <button
                  className={`btn-create-pr${prCreating ? ' btn-create-pr--loading' : ''}`}
                  onClick={onCreatePr}
                  disabled={prCreating || pushResults.every(r => !r.branchName)}
                >
                  {prCreating ? '생성 중...' : 'Draft PR 생성'}
                </button>
              </div>
            ) : (
              /* Step 1: Push branch button */
              <button
                className={`btn-create-pr${pushCreating ? ' btn-create-pr--loading' : ''}`}
                onClick={onPushBranch}
                disabled={pushCreating}
              >
                {pushCreating ? '푸시 중...' : '브랜치 푸시'}
              </button>
            )}
          </div>
        </div>
      )}

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
          onJiraCreated={onJiraCreated}
        />
      )}
    </div>
  )
}
