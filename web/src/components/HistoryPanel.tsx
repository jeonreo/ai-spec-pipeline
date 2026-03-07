import { useEffect, useState } from 'react'
import { deleteHistoryItem, fetchHistory, fetchHistoryDetail, type HistoryItem } from '../api'
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
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [error, setError] = useState('')

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  function load(nextPage: number, nextDate: string) {
    setLoading(true)
    setError('')
    fetchHistory(nextPage, PAGE_SIZE, nextDate || undefined)
      .then(data => {
        setItems(data.items)
        setTotal(data.total)
      })
      .catch(e => setError(e instanceof Error ? e.message : '히스토리를 불러오지 못했습니다.'))
      .finally(() => setLoading(false))
  }

  useEffect(() => { load(1, '') }, [])

  function handleDateChange(value: string) {
    setDate(value)
    setPage(1)
    load(1, value)
  }

  function handlePage(nextPage: number) {
    setPage(nextPage)
    load(nextPage, date)
  }

  async function handleLoad(id: string) {
    setLoadingId(id)
    setError('')
    try {
      const detail = await fetchHistoryDetail(id)
      const outputs: Partial<Record<Tab, string>> = {}
      for (const tab of STAGE_ORDER) {
        if (detail.outputs[tab]) outputs[tab] = detail.outputs[tab]
      }
      onRestore(detail.inputText, outputs)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : '히스토리를 불러오지 못했습니다.')
    } finally {
      setLoadingId(null)
    }
  }

  async function handleDelete(id: string) {
    if (!window.confirm('이 히스토리를 삭제할까요?')) return

    setDeletingId(id)
    setError('')
    try {
      await deleteHistoryItem(id)
      const nextTotal = Math.max(0, total - 1)
      const nextPage = Math.min(page, Math.max(1, Math.ceil(nextTotal / PAGE_SIZE)))
      setPage(nextPage)
      load(nextPage, date)
    } catch (e) {
      setError(e instanceof Error ? e.message : '히스토리 삭제에 실패했습니다.')
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <div className="history-overlay" onClick={onClose}>
      <div className="history-panel" onClick={e => e.stopPropagation()}>
        <div className="history-header">
          <span>히스토리</span>
          <button onClick={onClose}>×</button>
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
          {error && <div className="history-error">{error}</div>}
          {loading && <div className="history-empty">불러오는 중...</div>}
          {!loading && items.length === 0 && (
            <div className="history-empty">해당하는 히스토리가 없습니다.</div>
          )}
          {items.map(item => (
            <div key={item.id} className="history-item">
              <div className="history-item-meta">
                <span className="history-time">{parseTimestamp(item.id)}</span>
                <span className="history-stages">
                  {item.stages.map(s => s.replace(/\.\w+$/, '')).join(' / ')}
                </span>
              </div>
              <div className="history-preview">{item.inputPreview}</div>
              <div className="history-item-actions">
                <button
                  className="btn-history-delete"
                  disabled={loadingId === item.id || deletingId === item.id}
                  onClick={() => handleDelete(item.id)}
                >
                  {deletingId === item.id ? '삭제 중...' : '삭제'}
                </button>
                <button
                  className="btn-history-load"
                  disabled={loadingId === item.id || deletingId === item.id}
                  onClick={() => handleLoad(item.id)}
                >
                  {loadingId === item.id ? '불러오는 중...' : '불러오기'}
                </button>
              </div>
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
