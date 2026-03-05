import { useEffect, useState } from 'react'
import { fetchHistory, fetchHistoryDetail, HistoryItem } from '../api'
import type { Tab } from '../App'

interface Props {
  onRestore: (inputText: string, outputs: Partial<Record<Tab, string>>) => void
  onClose: () => void
}

const STAGE_ORDER: Tab[] = ['intake', 'spec', 'jira', 'qa', 'design']
const PAGE_SIZE = 20

function parseTimestamp(id: string): string {
  const m = id.match(/^(\d{4})(\d{2})(\d{2})-(\d{2})(\d{2})(\d{2})/)
  if (!m) return id
  return `${m[1]}-${m[2]}-${m[3]} ${m[4]}:${m[5]}:${m[6]}`
}

export default function HistoryPanel({ onRestore, onClose }: Props) {
  const [items, setItems] = useState<HistoryItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [date, setDate] = useState('')
  const [loading, setLoading] = useState(true)
  const [loadingId, setLoadingId] = useState<string | null>(null)

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  function load(p: number, d: string) {
    setLoading(true)
    fetchHistory(p, PAGE_SIZE, d || undefined)
      .then(data => {
        setItems(data.items)
        setTotal(data.total)
      })
      .finally(() => setLoading(false))
  }

  useEffect(() => { load(1, '') }, [])

  function handleDateChange(val: string) {
    setDate(val)
    setPage(1)
    load(1, val)
  }

  function handlePage(p: number) {
    setPage(p)
    load(p, date)
  }

  async function handleLoad(id: string) {
    setLoadingId(id)
    try {
      const detail = await fetchHistoryDetail(id)
      const outputs: Partial<Record<Tab, string>> = {}
      for (const tab of STAGE_ORDER) {
        if (detail.outputs[tab]) outputs[tab] = detail.outputs[tab]
      }
      onRestore(detail.inputText, outputs)
      onClose()
    } finally {
      setLoadingId(null)
    }
  }

  return (
    <div className="history-overlay" onClick={onClose}>
      <div className="history-panel" onClick={e => e.stopPropagation()}>
        <div className="history-header">
          <span>히스토리</span>
          <button onClick={onClose}>✕</button>
        </div>

        <div className="history-filter">
          <input
            type="date"
            value={date}
            onChange={e => handleDateChange(e.target.value)}
          />
          {date && (
            <button className="btn-clear-date" onClick={() => handleDateChange('')}>
              초기화
            </button>
          )}
          <span className="history-total">{total}건</span>
        </div>

        <div className="history-body">
          {loading && <div className="history-empty">불러오는 중…</div>}
          {!loading && items.length === 0 && (
            <div className="history-empty">해당하는 히스토리가 없습니다.</div>
          )}
          {items.map(item => (
            <div key={item.id} className="history-item">
              <div className="history-item-meta">
                <span className="history-time">{parseTimestamp(item.id)}</span>
                <span className="history-stages">
                  {item.stages.map(s => s.replace(/\.\w+$/, '')).join(' · ')}
                </span>
              </div>
              <div className="history-preview">{item.inputPreview}</div>
              <button
                className="btn-history-load"
                disabled={loadingId === item.id}
                onClick={() => handleLoad(item.id)}
              >
                {loadingId === item.id ? '불러오는 중…' : '불러오기'}
              </button>
            </div>
          ))}
        </div>

        {totalPages > 1 && (
          <div className="history-pagination">
            <button disabled={page <= 1} onClick={() => handlePage(page - 1)}>‹</button>
            <span>{page} / {totalPages}</span>
            <button disabled={page >= totalPages} onClick={() => handlePage(page + 1)}>›</button>
          </div>
        )}
      </div>
    </div>
  )
}
