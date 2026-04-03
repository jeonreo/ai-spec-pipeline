import { useEffect, useMemo, useState } from 'react'
import { fetchWorkflowDetail, fetchWorkflows, rerunWorkflowStage, type WorkflowDetail, type WorkflowListItem } from '../api'

interface Props {
  onClose: () => void
  initialWorkflowId?: string
}

function formatDate(value?: string): string {
  if (!value) return '-'
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString()
}

const STAGE_ORDER = ['intake', 'spec', 'jira']

export default function WorkflowPanel({ onClose, initialWorkflowId }: Props) {
  const [items, setItems] = useState<WorkflowListItem[]>([])
  const [selectedId, setSelectedId] = useState(initialWorkflowId ?? '')
  const [detail, setDetail] = useState<WorkflowDetail | null>(null)
  const [loadingList, setLoadingList] = useState(true)
  const [loadingDetail, setLoadingDetail] = useState(false)
  const [rerunStage, setRerunStage] = useState('')
  const [error, setError] = useState('')

  async function loadList(preferredId?: string) {
    setLoadingList(true)
    setError('')
    try {
      const data = await fetchWorkflows()
      setItems(data.items)
      const nextId = preferredId || selectedId || data.items[0]?.id || ''
      if (nextId) setSelectedId(nextId)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'workflow를 불러오지 못했습니다.')
    } finally {
      setLoadingList(false)
    }
  }

  async function loadDetail(id: string) {
    if (!id) return
    setLoadingDetail(true)
    setError('')
    try {
      const data = await fetchWorkflowDetail(id)
      setDetail(data)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'workflow 상세를 불러오지 못했습니다.')
    } finally {
      setLoadingDetail(false)
    }
  }

  useEffect(() => {
    loadList(initialWorkflowId)
  }, [initialWorkflowId])

  useEffect(() => {
    if (selectedId) loadDetail(selectedId)
  }, [selectedId])

  const orderedStages = useMemo(() => {
    const stages = detail?.workflow.stages ?? {}
    const known = STAGE_ORDER.filter(stage => stages[stage])
    const extra = Object.keys(stages).filter(stage => !known.includes(stage))
    return [...known, ...extra]
  }, [detail])

  async function handleRerun(stage?: string) {
    if (!selectedId) return
    setRerunStage(stage ?? detail?.workflow.currentStage ?? '')
    setError('')
    try {
      await rerunWorkflowStage(selectedId, stage)
      await loadDetail(selectedId)
      await loadList(selectedId)
    } catch (e) {
      setError(e instanceof Error ? e.message : '재실행 요청에 실패했습니다.')
    } finally {
      setRerunStage('')
    }
  }

  return (
    <div className="workflow-overlay" onClick={onClose}>
      <div className="workflow-panel" onClick={e => e.stopPropagation()}>
        <div className="workflow-header">
          <span>Slack Workflows</span>
          <div className="workflow-header-actions">
            <button className="btn-header" onClick={() => loadList(selectedId)}>새로고침</button>
            <button onClick={onClose}>횞</button>
          </div>
        </div>

        <div className="workflow-body">
          <aside className="workflow-list">
            {loadingList && <div className="workflow-empty">불러오는 중...</div>}
            {!loadingList && items.length === 0 && <div className="workflow-empty">workflow가 없습니다.</div>}
            {items.map(item => (
              <button
                key={item.id}
                className={`workflow-list-item${selectedId === item.id ? ' workflow-list-item--active' : ''}`}
                onClick={() => setSelectedId(item.id)}
              >
                <div className="workflow-list-top">
                  <span className="workflow-list-id">{item.id}</span>
                  <span className="workflow-list-status">{item.status}</span>
                </div>
                <div className="workflow-list-stage">{item.currentStage}</div>
                <div className="workflow-list-preview">{item.requestPreview}</div>
                <div className="workflow-list-meta">
                  <span>{item.requestUserName || 'unknown'}</span>
                  {item.jiraIssueKey && <span>{item.jiraIssueKey}</span>}
                </div>
              </button>
            ))}
          </aside>

          <section className="workflow-detail">
            {error && <div className="workflow-error">{error}</div>}
            {loadingDetail && <div className="workflow-empty">상세 불러오는 중...</div>}
            {!loadingDetail && !detail && <div className="workflow-empty">선택된 workflow가 없습니다.</div>}
            {!loadingDetail && detail && (
              <>
                <div className="workflow-summary">
                  <div className="workflow-summary-row">
                    <span className="workflow-summary-title">{detail.workflow.id}</span>
                    <span className="workflow-summary-chip">{detail.workflow.status}</span>
                    <span className="workflow-summary-chip">{detail.workflow.currentStage}</span>
                  </div>
                  <div className="workflow-summary-grid">
                    <span>Created: {formatDate(detail.workflow.createdAt)}</span>
                    <span>Updated: {formatDate(detail.workflow.updatedAt)}</span>
                    <span>Slack DM: {detail.workflow.slack.channelId || '-'}</span>
                    <span>Jira Draft: {detail.workflow.jiraDraft.projectKey || '-'} / {detail.workflow.jiraDraft.issueTypeName || '-'}</span>
                    {detail.workflow.jiraResult && (
                      <a href={detail.workflow.jiraResult.issueUrl} target="_blank" rel="noreferrer">
                        Jira: {detail.workflow.jiraResult.issueKey}
                      </a>
                    )}
                  </div>
                  <pre className="workflow-request">{detail.workflow.requestText}</pre>
                  <div className="workflow-rerun-row">
                    <button
                      className="btn-workflow-rerun"
                      disabled={rerunStage === detail.workflow.currentStage}
                      onClick={() => handleRerun(detail.workflow.currentStage)}
                    >
                      {rerunStage === detail.workflow.currentStage ? '재실행 중...' : `현재 단계 재실행 (${detail.workflow.currentStage})`}
                    </button>
                  </div>
                </div>

                <div className="workflow-stage-grid">
                  {orderedStages.map(stage => {
                    const stageState = detail.workflow.stages[stage]
                    return (
                      <div key={stage} className="workflow-stage-card">
                        <div className="workflow-stage-card-top">
                          <span className="workflow-stage-name">{stage}</span>
                          <span className="workflow-stage-status">{stageState.status}</span>
                        </div>
                        <div className="workflow-stage-meta">
                          <span>Started: {formatDate(stageState.startedAt)}</span>
                          <span>Done: {formatDate(stageState.completedAt)}</span>
                        </div>
                        {stageState.lastError && <div className="workflow-stage-error">{stageState.lastError}</div>}
                        {stageState.lastFeedback && (
                          <div className="workflow-stage-feedback">
                            <strong>Feedback</strong>
                            <pre>{stageState.lastFeedback}</pre>
                          </div>
                        )}
                        {(stageState.reviewerDecision || stageState.reviewerSummary) && (
                          <div className="workflow-stage-feedback">
                            <strong>Reviewer</strong>
                            <pre>{`Decision: ${stageState.reviewerDecision || 'review_unavailable'}\n${stageState.reviewerSummary || ''}`}</pre>
                          </div>
                        )}
                        {stageState.reviewerPreview && (
                          <pre className="workflow-stage-preview">{stageState.reviewerPreview}</pre>
                        )}
                        {stageState.outputPreview && <pre className="workflow-stage-preview">{stageState.outputPreview}</pre>}
                        <button
                          className="btn-workflow-stage"
                          disabled={rerunStage === stage}
                          onClick={() => handleRerun(stage)}
                        >
                          {rerunStage === stage ? '재실행 중...' : '이 단계 재실행'}
                        </button>
                      </div>
                    )
                  })}
                </div>

                <div className="workflow-output-list">
                  {orderedStages.map(stage => (
                    <div key={stage} className="workflow-output-card">
                      <div className="workflow-output-title">{stage}</div>
                      <pre>{detail.outputs[stage] || '(empty)'}</pre>
                    </div>
                  ))}
                </div>
              </>
            )}
          </section>
        </div>
      </div>
    </div>
  )
}
