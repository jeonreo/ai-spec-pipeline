import { Tab, RunState } from '../App'

const BASE_STAGES: { tab: Tab; label: string; file: string; cls: string }[] = [
  { tab: 'intake', label: 'Intake', file: 'intake.md', cls: 'btn-intake' },
  { tab: 'spec',   label: 'Spec',   file: 'spec.md',   cls: 'btn-spec'   },
]

const DELIVERY_STAGES: { tab: Tab; label: string; file: string; cls: string }[] = [
  { tab: 'jira',   label: 'Jira',   file: 'jira.json',  cls: 'btn-delivery' },
  { tab: 'qa',     label: 'QA',     file: 'qa.md',      cls: 'btn-delivery' },
  { tab: 'design', label: 'Design', file: 'design.html', cls: 'btn-design'  },
]

const STEPPER_NODES = [
  { label: 'Intake', tabs: ['intake'] as Tab[] },
  { label: 'Spec',   tabs: ['spec']   as Tab[] },
  { label: 'Jira / QA / Design', tabs: ['jira', 'qa', 'design'] as Tab[] },
]

interface Props {
  input: string
  onInputChange: (v: string) => void
  onRun: (tab: Tab) => void
  onRunParallel: () => void
  runStates: Record<Tab, RunState>
  stale: Record<Tab, boolean>
}

function StageButton({ tab, label, file, cls, state, stale, onRun }: {
  tab: Tab; label: string; file: string; cls: string
  state: RunState; stale: boolean; onRun: (tab: Tab) => void
}) {
  const running = state === 'running'
  const done = state === 'done'
  return (
    <button
      className={`btn ${cls}${state === 'failed' ? ' btn-failed' : ''}`}
      onClick={() => onRun(tab)}
      disabled={running}
    >
      <span className="btn-label">{label}{stale && done && <span className="stale-badge">stale</span>}</span>
      <span className="btn-file">
        {running ? '실행 중...' : done ? `재실행 → ${file}` : `→ ${file}`}
      </span>
    </button>
  )
}

function nodeState(tabs: Tab[], runStates: Record<Tab, RunState>, stale: Record<Tab, boolean>): 'idle' | 'running' | 'done' | 'stale' | 'failed' {
  if (tabs.some(t => runStates[t] === 'running')) return 'running'
  if (tabs.some(t => stale[t] && runStates[t] === 'done')) return 'stale'
  if (tabs.every(t => runStates[t] === 'done')) return 'done'
  if (tabs.some(t => runStates[t] === 'failed')) return 'failed'
  return 'idle'
}

function Stepper({ runStates, stale }: { runStates: Record<Tab, RunState>; stale: Record<Tab, boolean> }) {
  return (
    <div className="stepper">
      {STEPPER_NODES.map((node, i) => {
        const s = nodeState(node.tabs, runStates, stale)
        return (
          <div key={node.label} className="stepper-node-wrap">
            <div className="stepper-item">
              <div className={`stepper-dot stepper-dot--${s}`}>
                {s === 'running' ? '…' : s === 'done' ? '✓' : s === 'stale' ? '!' : s === 'failed' ? '✕' : '○'}
              </div>
              <span className={`stepper-label stepper-label--${s}`}>{node.label}</span>
            </div>
            {i < STEPPER_NODES.length - 1 && <div className="stepper-connector" />}
          </div>
        )
      })}
    </div>
  )
}

export default function InputPanel({ input, onInputChange, onRun, onRunParallel, runStates, stale }: Props) {
  const parallelRunning = (['jira', 'qa', 'design'] as const).some(t => runStates[t] === 'running')
  const specDone = runStates['spec'] === 'done'

  return (
    <div className="input-panel">
      <label>요구사항 입력</label>
      <textarea
        value={input}
        onChange={(e) => onInputChange(e.target.value)}
        placeholder="자유 텍스트로 요구사항을 입력하세요."
      />
      <div className="btn-group">
        <span className="stage-group-label">기반 단계</span>
        {BASE_STAGES.map(s => (
          <StageButton key={s.tab} {...s} state={runStates[s.tab]} stale={stale[s.tab]} onRun={onRun} />
        ))}

        <div className="stage-divider" />
        <span className="stage-group-label">산출물</span>
        {DELIVERY_STAGES.map(s => (
          <StageButton key={s.tab} {...s} state={runStates[s.tab]} stale={stale[s.tab]} onRun={onRun} />
        ))}

        <button
          className="btn btn-parallel"
          onClick={onRunParallel}
          disabled={parallelRunning || !specDone}
        >
          {parallelRunning ? 'Jira · QA · Design 실행 중...' : '⚡ Jira · QA · Design 병렬 실행'}
        </button>
      </div>

      <Stepper runStates={runStates} stale={stale} />
    </div>
  )
}
