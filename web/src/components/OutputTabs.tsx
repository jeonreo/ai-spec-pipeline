import { Tab } from '../App'

const TABS: { key: Tab; label: string }[] = [
  { key: 'intake', label: 'intake.md'   },
  { key: 'spec',   label: 'spec.md'    },
  { key: 'jira',   label: 'jira.json'  },
  { key: 'qa',     label: 'qa.md'      },
  { key: 'design', label: 'design.html' },
]

interface Props {
  outputs: Record<Tab, string>
  activeTab: Tab
  onTabChange: (tab: Tab) => void
  onOutputChange: (tab: Tab, value: string) => void
  elapsed: Record<Tab, number | null>
  warnings: Record<Tab, string>
}

export default function OutputTabs({ outputs, activeTab, onTabChange, onOutputChange, elapsed, warnings }: Props) {
  const current = activeTab

  return (
    <div className="output-panel">
      <div className="tab-bar">
        <div className="tab-bar-tabs">
          {TABS.map(t => (
            <button
              key={t.key}
              className={`tab-btn${activeTab === t.key ? ' active' : ''}`}
              onClick={() => onTabChange(t.key)}
              title={warnings[t.key] || undefined}
            >
              {t.label}
              {elapsed[t.key] !== null && (
                <span className="tab-elapsed">{elapsed[t.key]!.toFixed(1)}s</span>
              )}
              {warnings[t.key] && <span className="tab-warning">⚠</span>}
            </button>
          ))}
        </div>
        <div className="tab-bar-actions">
          {current === 'design' && (
            <button
              className="btn-tab-action"
              onClick={() => {
                const blob = new Blob([outputs[current]], { type: 'text/html' })
                window.open(URL.createObjectURL(blob))
              }}
              disabled={!outputs[current]}
            >
              미리보기
            </button>
          )}
          <button
            className="btn-tab-action"
            onClick={() => navigator.clipboard.writeText(outputs[current])}
            disabled={!outputs[current]}
          >
            복사
          </button>
        </div>
      </div>
      <div className="tab-content">
        {warnings[current] && (
          <div className="warning-banner">⚠ {warnings[current]}</div>
        )}
        <textarea
          key={current}
          value={outputs[current]}
          onChange={(e) => onOutputChange(current, e.target.value)}
          placeholder={`${TABS.find(t => t.key === current)?.label} 결과가 여기에 표시됩니다.`}
        />
      </div>
    </div>
  )
}
