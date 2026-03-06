import { useState } from 'react'

interface JiraData {
  summary: string
  description: Record<string, string>
  acceptance_criteria: string[]
}

interface Props {
  content: string
  onChange: (val: string) => void
}

export default function JiraView({ content, onChange }: Props) {
  const [showRaw, setShowRaw] = useState(false)

  let data: JiraData | null = null
  if (content) {
    try { data = JSON.parse(content) } catch { /* show raw */ }
  }

  if (!content || showRaw || !data) {
    return (
      <div className="jira-raw-wrapper">
        {data && (
          <button className="btn-jira-toggle" onClick={() => setShowRaw(false)}>카드 뷰</button>
        )}
        <textarea
          value={content}
          onChange={e => onChange(e.target.value)}
          placeholder="jira.json 결과가 여기에 표시됩니다."
        />
      </div>
    )
  }

  return (
    <div className="jira-card-wrapper">
      <div className="jira-card">
        <div className="jira-card-header">
          <span className="jira-type-badge">Story</span>
          <h2 className="jira-summary">{data.summary}</h2>
          <button className="btn-jira-toggle" onClick={() => setShowRaw(true)}>JSON</button>
        </div>

        {data.description && Object.keys(data.description).length > 0 && (
          <div className="jira-section">
            <div className="jira-section-title">설명</div>
            <dl className="jira-desc-list">
              {Object.entries(data.description).map(([k, v]) => v ? (
                <div key={k} className="jira-desc-row">
                  <dt className="jira-desc-label">{k}</dt>
                  <dd className="jira-desc-value">{v}</dd>
                </div>
              ) : null)}
            </dl>
          </div>
        )}

        {data.acceptance_criteria && data.acceptance_criteria.length > 0 && (
          <div className="jira-section">
            <div className="jira-section-title">완료 조건 (AC)</div>
            <ul className="jira-ac-list">
              {data.acceptance_criteria.map((ac, i) => (
                <li key={i} className="jira-ac-item">
                  <span className="jira-ac-num">{i + 1}</span>
                  <span>{ac}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  )
}
