import { useEffect } from 'react'
import { Tab, RunState } from '../App'
import JiraView from './JiraView'
import DesignPackageView from './DesignPackageView'

interface Props {
  tab: Tab
  runState: RunState
  output: string
  specOutput: string
  elapsed: number | null
  warning: string
  onOutputChange: (val: string) => void
  onClose: () => void
  onRun: () => void
  specDone: boolean
  decisions: string
  onDecisionsChange: (v: string) => void
  onConfirmAndRun: () => void
  onSkipAndRun: () => void
  decisionsConfirmed: boolean
  jiraProjectKey: string
  jiraIssueTypeName: string
}

const DRAWER_META: Record<Tab, { label: string; icon: string; desc: string }> = {
  intake: { label: 'Intake Agent',  icon: '📥', desc: '비정형 입력 → 구조화된 요구사항 분석' },
  spec:   { label: 'Spec Agent',    icon: '📋', desc: 'Intake + 결정사항 → 기능 명세 (SSoT)' },
  jira:   { label: 'Jira Agent',    icon: '🎯', desc: 'Spec → Jira 이슈 초안 생성'           },
  qa:     { label: 'QA Agent',      icon: '🧪', desc: 'Spec → QA 테스트 케이스 작성'          },
  design: { label: 'Design Agent',  icon: '🎨', desc: 'Spec → 디자인 핸드오프 패키지'         },
}

function statusLabel(state: RunState) {
  switch (state) {
    case 'running': return '실행중'
    case 'done':    return '완료'
    case 'failed':  return '실패'
    default:        return '대기'
  }
}

function IntakeContentView({ content }: { content: string }) {
  interface QA { question: string; answer: string }
  interface Section { heading: string; body: string; qas: QA[] }

  function build(heading: string, lines: string[]): Section {
    if (heading !== '결정 필요') return { heading, body: lines.join('\n').trim(), qas: [] }
    const qas: QA[] = []
    let q: string | null = null
    for (const l of lines) {
      if (l.startsWith('Q.')) { q = l.slice(2).trim() }
      else if (l.startsWith('A.') && q !== null) { qas.push({ question: q, answer: l.slice(2).trim() }); q = null }
    }
    if (q !== null) qas.push({ question: q, answer: '' })
    return { heading, body: '', qas }
  }

  function parse(text: string): Section[] {
    if (!text.trim()) return []
    const sections: Section[] = []
    let cur: { heading: string; lines: string[] } | null = null
    for (const line of text.split('\n')) {
      if (line.startsWith('## ')) {
        if (cur) sections.push(build(cur.heading, cur.lines))
        cur = { heading: line.slice(3).trim(), lines: [] }
      } else if (cur) { cur.lines.push(line) }
    }
    if (cur) sections.push(build(cur.heading, cur.lines))
    return sections
  }

  const sections = parse(content)
  if (!sections.length) return <pre className="drawer-pre">{content}</pre>
  return (
    <div className="intake-view">
      {sections.map(s => (
        <div key={s.heading} className="intake-section">
          <div className="intake-section-heading">{s.heading}</div>
          {s.qas.length > 0 ? (
            <div className="intake-qas">
              {s.qas.map((qa, i) => (
                <div key={i} className={`intake-qa${qa.answer ? ' intake-qa--answered' : ' intake-qa--pending'}`}>
                  <div className="intake-q"><span className="intake-q-label">Q</span><span>{qa.question}</span></div>
                  <div className="intake-a">
                    <span className="intake-a-label">A</span>
                    {qa.answer
                      ? <span className="intake-a-value">{qa.answer}</span>
                      : <span className="intake-a-empty">미결정</span>}
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

function SpecContentView({ content, onChange }: { content: string; onChange: (v: string) => void }) {
  function parse(spec: string) {
    if (!spec.trim()) return []
    const sections: { heading: string; content: string }[] = []
    let cur: { heading: string; lines: string[] } | null = null
    for (const line of spec.split('\n')) {
      if (line.startsWith('## ')) {
        if (cur) sections.push({ heading: cur.heading, content: cur.lines.join('\n').trim() })
        cur = { heading: line.slice(3).trim(), lines: [] }
      } else if (cur) { cur.lines.push(line) }
    }
    if (cur) sections.push({ heading: cur.heading, content: cur.lines.join('\n').trim() })
    return sections
  }

  const sections = parse(content)
  if (!sections.length) return (
    <textarea className="drawer-textarea" value={content} onChange={e => onChange(e.target.value)} />
  )
  return (
    <div className="spec-sections">
      {sections.map(s => (
        <div key={s.heading} className="spec-section">
          <div className="spec-section-heading">{s.heading}</div>
          <div className="spec-section-content">{s.content || <span className="spec-section-empty">내용 없음</span>}</div>
        </div>
      ))}
    </div>
  )
}

export default function CardDetailDrawer({
  tab, runState, output, specOutput, elapsed, warning,
  onOutputChange, onClose, onRun, specDone,
  decisions, onDecisionsChange, onConfirmAndRun, onSkipAndRun, decisionsConfirmed,
  jiraProjectKey, jiraIssueTypeName,
}: Props) {
  const meta    = DRAWER_META[tab]
  const isDone    = runState === 'done'
  const isRunning = runState === 'running'
  const isFailed  = runState === 'failed'
  const canRun    = !isRunning && (tab === 'intake' || tab === 'spec' || specDone)

  // ESC to close
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [onClose])

  function renderContent() {
    if (isRunning) return <div className="drawer-empty">실행 중...</div>
    if (!output)   return <div className="drawer-empty">아직 실행되지 않았습니다.</div>
    if (tab === 'intake') return (
      <div className="intake-drawer-content">
        <IntakeContentView content={output} />
        <div className="intake-decision-section">
          <div className="intake-decision-label">
            <span className="intake-decision-icon">💬</span>
            <span>결정사항 입력</span>
            {decisionsConfirmed && <span className="intake-decision-confirmed-badge">확정됨</span>}
          </div>
          <textarea
            className="intake-decision-textarea"
            value={decisions}
            onChange={e => onDecisionsChange(e.target.value)}
            placeholder={"Intake 내용을 검토하고 결정사항을 입력하세요.\n예: 모바일 우선 개발, 다크모드 제외, MVP 범위 한정..."}
          />
          <div className="intake-decision-actions">
            <button
              className="btn-decision-confirm"
              onClick={() => { onConfirmAndRun(); onClose() }}
            >
              확정 후 자동 실행 →
            </button>
            <button
              className="btn-decision-skip"
              onClick={() => { onSkipAndRun(); onClose() }}
            >
              결정사항 없이 진행
            </button>
          </div>
        </div>
      </div>
    )
    if (tab === 'spec')   return <SpecContentView content={output} onChange={onOutputChange} />
    if (tab === 'jira')   return <JiraView content={output} onChange={onOutputChange} specContent={specOutput} initialProjectKey={jiraProjectKey} initialIssueTypeName={jiraIssueTypeName} />
    if (tab === 'qa')     return <textarea className="drawer-textarea" value={output} onChange={e => onOutputChange(e.target.value)} />
    if (tab === 'design') return <DesignPackageView content={output} onChange={onOutputChange} />
    return null
  }

  return (
    <>
      {/* Backdrop */}
      <div className="drawer-backdrop" onClick={onClose} />

      {/* Drawer */}
      <div className="card-detail-drawer">
        {/* Drawer header */}
        <div className="drawer-header">
          <div className="drawer-header-left">
            <span className="drawer-icon">{meta.icon}</span>
            <div className="drawer-title-block">
              <span className="drawer-title">{meta.label}</span>
              <span className="drawer-desc">{meta.desc}</span>
            </div>
          </div>
          <div className="drawer-header-right">
            <span className={`drawer-status-badge drawer-status-badge--${runState}`}>
              {isRunning && <span className="drawer-running-dots"><span /><span /><span /></span>}
              {statusLabel(runState)}
            </span>
            {elapsed !== null && <span className="drawer-elapsed">{elapsed.toFixed(1)}s</span>}
            {warning && <span className="drawer-warning" title={warning}>⚠ {warning}</span>}
            <button
              className={`btn-drawer-run${isRunning ? ' btn-drawer-run--running' : isFailed ? ' btn-drawer-run--failed' : isDone ? ' btn-drawer-run--done' : ''}`}
              onClick={onRun}
              disabled={!canRun}
            >
              {isRunning ? '실행 중...' : isFailed ? '↺ 재시도' : isDone ? '↺ 재실행' : '▶ 실행'}
            </button>
            <button className="drawer-close" onClick={onClose} title="닫기 (ESC)">✕</button>
          </div>
        </div>

        {/* Drawer body */}
        <div className="drawer-body">
          {renderContent()}
        </div>
      </div>
    </>
  )
}
