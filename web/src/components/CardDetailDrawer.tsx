import { useEffect, useState, useCallback } from 'react'
import { Tab, RunState } from '../App'
import JiraView from './JiraView'
import DesignPackageView from './DesignPackageView'
import { updatePolicy, fetchOriginalFile } from '../api'

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
  onJiraCreated?: (key: string) => void
}

const DRAWER_META: Record<Tab, { label: string; icon: string; desc: string }> = {
  intake:          { label: 'Intake Agent',  icon: '📥', desc: '비정형 입력 → 구조화된 요구사항 분석'           },
  spec:            { label: 'Spec Agent',    icon: '📋', desc: 'Intake + 결정사항 → 기능 명세 (SSoT)'          },
  jira:            { label: 'Jira Agent',    icon: '🎯', desc: 'Spec → Jira 이슈 초안 생성'                    },
  qa:              { label: 'QA Agent',      icon: '🧪', desc: 'Spec → QA 테스트 케이스 작성'                  },
  design:          { label: 'Design Agent',  icon: '🎨', desc: 'Spec → 디자인 핸드오프 패키지'                 },
  'code-analysis': { label: 'Code Agent',    icon: '🔍', desc: 'Spec + 코드베이스 → 변경 대상 파일 분석'       },
  patch:           { label: 'Patch Agent',   icon: '🩹', desc: 'Code Analysis → 실제 파일 변경 코드 생성'      },
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

interface DiffLine { type: 'same' | 'add' | 'remove'; content: string }

function computeDiff(original: string, updated: string): DiffLine[] {
  const a = original.split('\n')
  const b = updated.split('\n')
  // Fall back to simple display for very large files
  if (a.length > 600 || b.length > 600) {
    return b.map(line => ({ type: 'same', content: line }))
  }
  // LCS DP table
  const m = a.length, n = b.length
  const dp = Array.from({ length: m + 1 }, () => new Int32Array(n + 1))
  for (let i = 1; i <= m; i++)
    for (let j = 1; j <= n; j++)
      dp[i][j] = a[i - 1] === b[j - 1] ? dp[i - 1][j - 1] + 1 : Math.max(dp[i - 1][j], dp[i][j - 1])
  // Trace back
  const ops: DiffLine[] = []
  let i = m, j = n
  while (i > 0 || j > 0) {
    if (i > 0 && j > 0 && a[i - 1] === b[j - 1]) {
      ops.unshift({ type: 'same', content: a[i - 1] }); i--; j--
    } else if (j > 0 && (i === 0 || dp[i][j - 1] >= dp[i - 1][j])) {
      ops.unshift({ type: 'add', content: b[j - 1] }); j--
    } else {
      ops.unshift({ type: 'remove', content: a[i - 1] }); i--
    }
  }
  return ops
}

function PatchFileDiff({ file, repo }: { file: { repo?: string; path: string; content: string; comment?: string }; repo: string }) {
  const [mode, setMode]           = useState<'code' | 'diff'>('code')
  const [original, setOriginal]   = useState<string | null>(null)
  const [loading, setLoading]     = useState(false)
  const [diffLines, setDiffLines] = useState<DiffLine[] | null>(null)

  const loadDiff = useCallback(async () => {
    if (original !== null) { setMode('diff'); return }
    setLoading(true)
    const orig = await fetchOriginalFile(repo, file.path)
    setLoading(false)
    if (orig === null) {
      // New file — show all lines as added
      setOriginal('')
      setDiffLines(file.content.split('\n').map(line => ({ type: 'add', content: line })))
    } else {
      setOriginal(orig)
      setDiffLines(computeDiff(orig, file.content))
    }
    setMode('diff')
  }, [original, repo, file.path, file.content])

  const addCount    = diffLines?.filter(l => l.type === 'add').length ?? 0
  const removeCount = diffLines?.filter(l => l.type === 'remove').length ?? 0

  return (
    <details className="patch-file">
      <summary className="patch-file-summary">
        <span className="patch-file-path">{file.path}</span>
        {file.comment && <span className="patch-file-comment">{file.comment}</span>}
        {diffLines && (
          <span className="patch-diff-stats">
            {addCount > 0 && <span className="patch-stat-add">+{addCount}</span>}
            {removeCount > 0 && <span className="patch-stat-remove">-{removeCount}</span>}
          </span>
        )}
      </summary>
      <div className="patch-file-toolbar">
        <button
          className={`patch-view-btn${mode === 'code' ? ' patch-view-btn--active' : ''}`}
          onClick={() => setMode('code')}
        >코드</button>
        <button
          className={`patch-view-btn${mode === 'diff' ? ' patch-view-btn--active' : ''}`}
          onClick={loadDiff}
          disabled={loading}
        >{loading ? '로딩...' : 'Diff'}</button>
      </div>
      {mode === 'code' && <pre className="patch-file-content">{file.content}</pre>}
      {mode === 'diff' && diffLines && (
        <div className="patch-diff-view">
          {diffLines.map((line, idx) => (
            <div key={idx} className={`patch-diff-line patch-diff-line--${line.type}`}>
              <span className="patch-diff-gutter">{line.type === 'add' ? '+' : line.type === 'remove' ? '-' : ' '}</span>
              <span className="patch-diff-text">{line.content}</span>
            </div>
          ))}
        </div>
      )}
    </details>
  )
}

function PatchView({ content }: { content: string }) {
  interface PatchFile { repo?: string; path: string; content: string; comment?: string }

  let patches: PatchFile[] = []
  let parseError = ''
  try {
    patches = JSON.parse(content)
    if (!Array.isArray(patches)) { patches = []; parseError = 'JSON array 형식이 아닙니다.' }
  } catch (e) {
    parseError = content ? '아직 완성되지 않은 JSON입니다. 스트리밍 완료 후 확인하세요.' : ''
  }

  if (parseError) return <div className="drawer-empty">{parseError}</div>
  if (!patches.length) return <div className="drawer-empty">변경 파일 없음</div>

  const byRepo = patches.reduce<Record<string, PatchFile[]>>((acc, p) => {
    const key = p.repo ?? 'unknown'
    ;(acc[key] ??= []).push(p)
    return acc
  }, {})

  return (
    <div className="patch-view">
      <div className="patch-summary">
        총 {patches.length}개 파일 변경
        {Object.entries(byRepo).map(([repo, files]) => (
          <span key={repo} className="patch-repo-badge">{repo.toUpperCase()} {files.length}개</span>
        ))}
      </div>
      {Object.entries(byRepo).map(([repo, files]) => (
        <div key={repo} className="patch-repo-group">
          <div className="patch-repo-label">{repo.toUpperCase()} 저장소</div>
          {files.map((f, i) => (
            <PatchFileDiff key={i} file={f} repo={repo} />
          ))}
        </div>
      ))}
    </div>
  )
}

export default function CardDetailDrawer({
  tab, runState, output, specOutput, elapsed, warning,
  onOutputChange, onClose, onRun, specDone,
  decisions, onDecisionsChange, onConfirmAndRun, onSkipAndRun, decisionsConfirmed,
  jiraProjectKey, jiraIssueTypeName, onJiraCreated,
}: Props) {
  const meta    = DRAWER_META[tab]
  const isDone    = runState === 'done'
  const isRunning = runState === 'running'
  const isFailed  = runState === 'failed'
  const canRun    = !isRunning && (tab === 'intake' || tab === 'spec' || specDone)

  const [policyUpdating, setPolicyUpdating] = useState(false)
  const [policyMsg, setPolicyMsg] = useState<string | null>(null)

  const canUpdatePolicy = isDone && output && (tab === 'intake' || tab === 'spec')

  async function handlePolicyUpdate() {
    if (!output) return
    setPolicyUpdating(true)
    setPolicyMsg(null)
    try {
      await updatePolicy(output)
      setPolicyMsg('정책 반영 완료')
    } catch {
      setPolicyMsg('반영 실패')
    } finally {
      setPolicyUpdating(false)
      setTimeout(() => setPolicyMsg(null), 3000)
    }
  }

  // ESC to close
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [onClose])

  function renderContent() {
    if (!output && isRunning) return <div className="drawer-empty">실행 중...</div>
    if (!output)              return <div className="drawer-empty">아직 실행되지 않았습니다.</div>
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
    if (tab === 'spec')            return <SpecContentView content={output} onChange={onOutputChange} />
    if (tab === 'jira')            return <JiraView content={output} onChange={onOutputChange} specContent={specOutput} initialProjectKey={jiraProjectKey} initialIssueTypeName={jiraIssueTypeName} onCreated={onJiraCreated} />
    if (tab === 'qa')              return <textarea className="drawer-textarea" value={output} onChange={e => onOutputChange(e.target.value)} />
    if (tab === 'design')          return <DesignPackageView content={output} onChange={onOutputChange} />
    if (tab === 'code-analysis')   return <SpecContentView content={output} onChange={onOutputChange} />
    if (tab === 'patch')           return <PatchView content={output} />
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
            {canUpdatePolicy && (
              <button
                className="btn-policy-update"
                onClick={handlePolicyUpdate}
                disabled={policyUpdating}
                title="현재 결과물을 policy.md에 반영"
              >
                {policyUpdating ? '반영 중...' : policyMsg ?? '📌 정책 반영'}
              </button>
            )}
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
