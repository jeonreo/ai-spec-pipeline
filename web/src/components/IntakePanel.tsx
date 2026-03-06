import { useState, useEffect } from 'react'
import { RunState } from '../App'

interface Props {
  content: string
  onChange: (v: string) => void
  runState: RunState
  elapsed: number | null
  stale: boolean
  specDone: boolean
  onRun: () => void
}

interface QA {
  question: string
  answer: string   // 비어있으면 미결정
}

interface Section {
  heading: string
  body: string
  qas: QA[]        // heading이 '결정 필요'일 때만 채워짐
}

function parseIntake(text: string): Section[] {
  if (!text.trim()) return []
  const sections: Section[] = []
  let current: { heading: string; lines: string[] } | null = null

  for (const line of text.split('\n')) {
    if (line.startsWith('## ')) {
      if (current) sections.push(buildSection(current.heading, current.lines))
      current = { heading: line.slice(3).trim(), lines: [] }
    } else if (current) {
      current.lines.push(line)
    }
  }
  if (current) sections.push(buildSection(current.heading, current.lines))
  return sections
}

function buildSection(heading: string, lines: string[]): Section {
  if (heading !== '결정 필요') {
    return { heading, body: lines.join('\n').trim(), qas: [] }
  }

  const qas: QA[] = []
  let currentQ: string | null = null
  for (const line of lines) {
    if (line.startsWith('Q.')) {
      currentQ = line.slice(2).trim()
    } else if (line.startsWith('A.') && currentQ !== null) {
      qas.push({ question: currentQ, answer: line.slice(2).trim() })
      currentQ = null
    }
  }
  // A. 없이 Q.만 있는 경우
  if (currentQ !== null) qas.push({ question: currentQ, answer: '' })

  return { heading, body: '', qas }
}

function IntakeView({ content }: { content: string }) {
  const sections = parseIntake(content)
  if (!sections.length) return <pre className="intake-panel-content">{content}</pre>

  return (
    <div className="intake-view">
      {sections.map(s => (
        <div key={s.heading} className="intake-section">
          <div className="intake-section-heading">{s.heading}</div>
          {s.qas.length > 0 ? (
            <div className="intake-qas">
              {s.qas.map((qa, i) => (
                <div key={i} className={`intake-qa${qa.answer ? ' intake-qa--answered' : ' intake-qa--pending'}`}>
                  <div className="intake-q">
                    <span className="intake-q-label">Q</span>
                    <span>{qa.question}</span>
                  </div>
                  <div className="intake-a">
                    <span className="intake-a-label">A</span>
                    {qa.answer
                      ? <span className="intake-a-value">{qa.answer}</span>
                      : <span className="intake-a-empty">미결정 — 답변을 입력하세요</span>
                    }
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="intake-section-body">{s.body}</div>
          )}
        </div>
      ))}
    </div>
  )
}

export default function IntakePanel({ content, onChange, runState, elapsed, stale, specDone, onRun }: Props) {
  const [editMode, setEditMode] = useState(false)
  const [collapsed, setCollapsed] = useState(specDone)

  useEffect(() => {
    setCollapsed(specDone)
  }, [specDone])

  const isDone    = runState === 'done'
  const isRunning = runState === 'running'

  function handleCopy() {
    navigator.clipboard.writeText(content)
  }

  return (
    <div className={`intake-panel${collapsed ? ' intake-panel--collapsed' : ''}`}>
      <div className="panel-header">
        <div className="panel-header-left">
          <span className="panel-title">Intake</span>
          {elapsed !== null && <span className="elapsed-badge">{elapsed.toFixed(1)}s</span>}
          {isDone && stale && <span className="stage-stale-badge">stale</span>}
        </div>
        <div className="panel-header-right">
          {isDone && !collapsed && (
            <>
              <button className="btn-icon-action" onClick={handleCopy} title="복사">복사</button>
              <button className="btn-icon-action" onClick={() => setEditMode(v => !v)}>
                {editMode ? '뷰' : '편집'}
              </button>
            </>
          )}
          <button className="btn-collapse" onClick={() => setCollapsed(v => !v)} title={collapsed ? '펼치기' : '접기'}>
            {collapsed ? '▾' : '▴'}
          </button>
          <button
            className={`btn-run-spec${isRunning ? ' btn-run-spec--running' : ''}`}
            onClick={onRun}
            disabled={isRunning}
          >
            {isRunning ? 'Intake 생성 중...' : isDone ? '↺ Regenerate' : 'Generate Intake'}
          </button>
        </div>
      </div>

      {!collapsed && (
        <div className="intake-panel-body">
          {isRunning || editMode || (content && !isDone) ? (
            <textarea
              className="spec-textarea"
              value={content}
              onChange={e => onChange(e.target.value)}
              placeholder="Intake 생성 중..."
            />
          ) : isDone && content ? (
            <IntakeView content={content} />
          ) : (
            <div className="spec-placeholder">
              <div className="spec-placeholder-icon">📥</div>
              <p className="spec-placeholder-title">Intake</p>
              <p className="spec-placeholder-sub">Sources 입력 후 Intake를 실행하세요.</p>
              <p className="spec-placeholder-note">비정형 입력을 구조화된 문제 정의로 변환합니다.</p>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
