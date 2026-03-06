import { useState } from 'react'
import { RunState } from '../App'

interface Props {
  content: string
  onChange: (v: string) => void
  runState: RunState
  elapsed: number | null
  warning: string
  stale: boolean
  onRun: () => void
}

interface Section {
  heading: string
  content: string
}

function parseSpecSections(spec: string): Section[] {
  if (!spec.trim()) return []
  const sections: Section[] = []
  let current: { heading: string; lines: string[] } | null = null
  for (const line of spec.split('\n')) {
    if (line.startsWith('## ')) {
      if (current) sections.push({ heading: current.heading, content: current.lines.join('\n').trim() })
      current = { heading: line.slice(3).trim(), lines: [] }
    } else if (current) {
      current.lines.push(line)
    }
  }
  if (current) sections.push({ heading: current.heading, content: current.lines.join('\n').trim() })
  return sections
}

export default function SpecPanel({ content, onChange, runState, elapsed, warning, stale, onRun }: Props) {
  const [editMode, setEditMode] = useState(false)

  const isDone    = runState === 'done'
  const isRunning = runState === 'running'
  const sections  = isDone && !editMode ? parseSpecSections(content) : []

  function handleCopy() {
    navigator.clipboard.writeText(content)
  }

  return (
    <div className="spec-panel">
      <div className="panel-header">
        <div className="panel-header-left">
          <span className="panel-title">Decision Spec</span>
          {isDone && <span className="panel-badge">Hub</span>}
          {elapsed !== null && <span className="elapsed-badge">{elapsed.toFixed(1)}s</span>}
          {isDone && stale && <span className="stage-stale-badge">stale</span>}
          {warning && <span className="warning-dot" title={warning}>⚠</span>}
        </div>
        <div className="panel-header-right">
          {isDone && (
            <>
              <button className="btn-icon-action" onClick={handleCopy} title="복사">복사</button>
              <button className="btn-icon-action" onClick={() => setEditMode(v => !v)}>
                {editMode ? '섹션 뷰' : '편집'}
              </button>
            </>
          )}
          <button
            className={`btn-run-spec${isRunning ? ' btn-run-spec--running' : ''}`}
            onClick={onRun}
            disabled={isRunning}
          >
            {isRunning ? 'Spec 생성 중...' : isDone ? '↺ Regenerate' : 'Generate Spec'}
          </button>
        </div>
      </div>

      <div className="spec-body">
        {isRunning || editMode || (content && !isDone) ? (
          <textarea
            className="spec-textarea"
            value={content}
            onChange={e => onChange(e.target.value)}
            placeholder="Spec 생성 중..."
          />
        ) : isDone && sections.length > 0 ? (
          <div className="spec-sections">
            {sections.map(s => (
              <div key={s.heading} className="spec-section">
                <div className="spec-section-heading">{s.heading}</div>
                <div className="spec-section-content">
                  {s.content || <span className="spec-section-empty">내용 없음</span>}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="spec-placeholder">
            <div className="spec-placeholder-icon">📋</div>
            <p className="spec-placeholder-title">Decision Spec</p>
            <p className="spec-placeholder-sub">Intake 실행 후 Spec을 생성하세요.</p>
            <p className="spec-placeholder-note">Spec은 Jira, QA, Design 산출물의 단일 진실 공급원입니다.</p>
          </div>
        )}
      </div>
    </div>
  )
}
