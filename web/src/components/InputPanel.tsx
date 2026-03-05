import { Tab, RunState } from '../App'

const STAGES: { tab: Tab; label: string; file: string; cls: string }[] = [
  { tab: 'intake', label: 'Intake', file: 'intake.md',  cls: 'btn-intake'   },
  { tab: 'spec',   label: 'Spec',   file: 'spec.md',    cls: 'btn-spec'     },
  { tab: 'jira',   label: 'Jira',   file: 'jira.json',  cls: 'btn-delivery' },
  { tab: 'qa',     label: 'QA',     file: 'qa.md',      cls: 'btn-delivery' },
]

interface Props {
  input: string
  onInputChange: (v: string) => void
  onRun: (tab: Tab) => void
  runStates: Record<Tab, RunState>
}

export default function InputPanel({ input, onInputChange, onRun, runStates }: Props) {
  return (
    <div className="input-panel">
      <label>요구사항 입력</label>
      <textarea
        value={input}
        onChange={(e) => onInputChange(e.target.value)}
        placeholder="자유 텍스트로 요구사항을 입력하세요."
      />
      <div className="btn-group">
        {STAGES.map(({ tab, label, file, cls }) => {
          const state = runStates[tab]
          const running = state === 'running'
          return (
            <button
              key={tab}
              className={`btn ${cls}${state === 'failed' ? ' btn-failed' : ''}`}
              onClick={() => onRun(tab)}
              disabled={running}
            >
              {running
                ? `${label} 실행 중...`
                : state === 'done'
                ? `${label} 재실행 → ${file}`
                : `${label} 실행 → ${file}`}
            </button>
          )
        })}
      </div>
    </div>
  )
}
