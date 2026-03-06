import { Tab, RunState } from '../App'

interface Props {
  input: string
  onInputChange: (v: string) => void
  onRun: (tab: Tab) => void
  runStates: Record<Tab, RunState>
  stale: Record<Tab, boolean>
}

export default function SourcePanel({ input, onInputChange, onRun, runStates, stale }: Props) {
  const intakeState   = runStates.intake
  const intakeDone    = intakeState === 'done'
  const intakeRunning = intakeState === 'running'

  return (
    <div className="source-panel">
      <div className="panel-header">
        <span className="panel-title">Sources</span>
      </div>

      <div className="source-body">
        <textarea
          className="source-textarea"
          value={input}
          onChange={e => onInputChange(e.target.value)}
          placeholder="요구사항, 슬랙 스레드, 미팅 노트 등을 자유롭게 입력하세요."
        />

        <div className="source-actions">
          <button
            className={`btn-stage${intakeRunning ? ' btn-stage--running' : ''}${intakeDone ? ' btn-stage--done' : ''}${intakeState === 'failed' ? ' btn-stage--failed' : ''}`}
            onClick={() => onRun('intake')}
            disabled={intakeRunning}
          >
            <span className="btn-stage-label">
              {intakeRunning ? 'Intake 실행 중...' : 'Generate Intake'}
            </span>
            {intakeDone && stale.intake && <span className="stage-stale-badge">stale</span>}
            <span className="btn-stage-file">→ intake.md</span>
          </button>
        </div>
      </div>
    </div>
  )
}
