import { useState } from 'react'
import { consolidateKnowledge } from '../api'

interface Props {
  knowledge: string
  decisions: string
  intakeOutput?: string
  onKnowledgeChange: (v: string) => void
}

interface QAPair { question: string; answer: string }

function extractQAPairs(intakeOutput: string): QAPair[] {
  const lines = intakeOutput.split('\n')
  let inSection = false
  const pairs: QAPair[] = []
  let pendingQ: string | null = null
  for (const line of lines) {
    if (line.startsWith('## ')) {
      inSection = line.startsWith('## 결정 필요')
      pendingQ = null
    } else if (inSection) {
      if (line.startsWith('Q.')) {
        pendingQ = line.slice(2).trim()
      } else if (line.startsWith('A.') && pendingQ !== null) {
        pairs.push({ question: pendingQ, answer: line.slice(2).trim() })
        pendingQ = null
      }
    }
  }
  if (pendingQ !== null) pairs.push({ question: pendingQ, answer: '' })
  return pairs
}

export default function ProjectKnowledgePanel({ knowledge, decisions, intakeOutput, onKnowledgeChange }: Props) {
  const [open, setOpen] = useState(false)
  const [consolidating, setConsolidating] = useState(false)
  const [error, setError] = useState('')

  function handleAppend() {
    const trimmed = decisions.trim()
    if (!trimmed && !intakeOutput) return

    const pairs = intakeOutput ? extractQAPairs(intakeOutput) : []
    const entryIndex = (knowledge.match(/### 결정 \(#\d+\)/g) ?? []).length + 1

    let entry: string
    if (pairs.length > 0) {
      const pairLines = pairs.map(p =>
        `- Q. ${p.question}\n  A. ${p.answer || '미결정'}`
      ).join('\n')
      entry = `### 결정 (#${entryIndex})\n${pairLines}`
      if (trimmed) entry += `\n\n추가 결정사항: ${trimmed}`
    } else {
      const lines = trimmed.split('\n').map(l => `- ${l.trim()}`).filter(l => l !== '- ')
      entry = `### 결정 (#${entryIndex})\n${lines.join('\n')}`
    }

    const separator = knowledge.trim() ? '\n\n' : ''
    onKnowledgeChange((knowledge + separator + entry).trimStart())
  }

  async function handleConsolidate() {
    if (!knowledge.trim() || consolidating) return
    setConsolidating(true)
    setError('')
    try {
      const result = await consolidateKnowledge(knowledge, (acc) => onKnowledgeChange(acc))
      onKnowledgeChange(result)
    } catch (e) {
      setError(e instanceof Error ? e.message : '정리 실패')
    } finally {
      setConsolidating(false)
    }
  }

  const lineCount = knowledge ? knowledge.split('\n').filter(l => l.trim()).length : 0

  return (
    <div className="sidebar-section">
      <div className="sidebar-section-header sidebar-section-header--toggle" onClick={() => setOpen(o => !o)}>
        <span className="sidebar-section-icon">🧠</span>
        <span className="sidebar-section-title">프로젝트 지식</span>
        {!open && lineCount > 0 && <span className="knowledge-count-badge">{lineCount}</span>}
        <span className="sidebar-section-chevron">{open ? '▾' : '▸'}</span>
      </div>

      {open && (
        <div className="knowledge-body">
          <textarea
            className="sidebar-textarea sidebar-textarea--knowledge"
            value={knowledge}
            onChange={e => onKnowledgeChange(e.target.value)}
            placeholder={"반복 원칙, 확정된 결정 등을 누적합니다.\n예:\n- 모바일 우선 개발\n- 결제 기능 MVP 제외"}
          />
          {error && <div className="knowledge-error">{error}</div>}
          <div className="knowledge-actions">
            <button
              className="btn-knowledge-append"
              onClick={handleAppend}
              disabled={!decisions.trim()}
              title={decisions.trim() ? '이번 결정사항을 지식에 추가' : '결정사항을 먼저 입력하세요'}
            >
              + 결정사항 추가
            </button>
            <button
              className="btn-knowledge-consolidate"
              onClick={handleConsolidate}
              disabled={consolidating || !knowledge.trim()}
            >
              {consolidating ? '정리 중...' : '✨ AI 정리'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
