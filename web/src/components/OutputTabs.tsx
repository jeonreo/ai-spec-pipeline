import { Tab } from '../App'

const TABS: { key: Tab; label: string }[] = [
  { key: 'intake', label: 'intake.md'  },
  { key: 'spec',   label: 'spec.md'   },
  { key: 'jira',   label: 'jira.json' },
  { key: 'qa',     label: 'qa.md'     },
]

interface Props {
  outputs: Record<Tab, string>
  activeTab: Tab
  onTabChange: (tab: Tab) => void
  onOutputChange: (tab: Tab, value: string) => void
}

export default function OutputTabs({ outputs, activeTab, onTabChange, onOutputChange }: Props) {
  const current = activeTab

  return (
    <div className="output-panel">
      <div className="tab-bar">
        {TABS.map(t => (
          <button
            key={t.key}
            className={`tab-btn${activeTab === t.key ? ' active' : ''}`}
            onClick={() => onTabChange(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>
      <div className="tab-content">
        <textarea
          key={current}
          value={outputs[current]}
          onChange={(e) => onOutputChange(current, e.target.value)}
          placeholder={`${TABS.find(t => t.key === current)?.label} 결과가 여기에 표시됩니다.`}
        />
        <div className="tab-actions">
          <button
            className="btn-copy"
            onClick={() => navigator.clipboard.writeText(outputs[current])}
            disabled={!outputs[current]}
          >
            복사
          </button>
        </div>
      </div>
    </div>
  )
}
