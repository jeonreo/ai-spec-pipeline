import { useState, useEffect } from 'react'
import {
  fetchJiraStatus, fetchJiraProjects, fetchJiraIssueTypes, createJiraTicket,
  JiraProject, JiraIssueType, JiraStatus, CreateJiraResult,
} from '../api'

interface JiraData {
  summary: string
  description: Record<string, string>
  acceptance_criteria: string[]
}

interface Props {
  content: string
  onChange: (val: string) => void
  specContent?: string
}

type CreateState = 'idle' | 'loading-projects' | 'ready' | 'loading-types' | 'creating' | 'done' | 'error'

export default function JiraView({ content, onChange, specContent }: Props) {
  const [showRaw, setShowRaw] = useState(false)
  const [status, setStatus] = useState<JiraStatus | null>(null)
  const [createState, setCreateState] = useState<CreateState>('idle')
  const [projects, setProjects] = useState<JiraProject[]>([])
  const [issueTypes, setIssueTypes] = useState<JiraIssueType[]>([])
  const [selectedProject, setSelectedProject] = useState('')
  const [selectedType, setSelectedType] = useState('')
  const [createError, setCreateError] = useState('')
  const [result, setResult] = useState<CreateJiraResult | null>(null)

  useEffect(() => {
    fetchJiraStatus().then(setStatus).catch(() => {})
  }, [])

  let data: JiraData | null = null
  if (content) {
    try { data = JSON.parse(content) } catch { /* show raw */ }
  }

  const hasDefaults = !!(status?.defaultProjectKey && status?.defaultIssueTypeName)

  // 기본값 있으면 원클릭 생성
  async function handleQuickCreate() {
    if (!data || !status) return
    setCreateState('creating')
    setCreateError('')
    try {
      const res = await createJiraTicket({
        projectKey:         status.defaultProjectKey,
        issueTypeName:      status.defaultIssueTypeName,
        summary:            data.summary,
        description:        data.description ?? {},
        acceptanceCriteria: data.acceptance_criteria ?? [],
        specContent,
      })
      setResult(res)
      setCreateState('done')
    } catch (e) {
      setCreateError(e instanceof Error ? e.message : '티켓 생성 실패')
      setCreateState('error')
    }
  }

  // 기본값 없으면 폼 열기
  async function handleOpenForm() {
    setCreateState('loading-projects')
    setCreateError('')
    setResult(null)
    try {
      const list = await fetchJiraProjects()
      setProjects(list)
      const firstKey = list[0]?.key ?? ''
      setSelectedProject(firstKey)
      setIssueTypes([])
      setSelectedType('')
      setCreateState('ready')
      if (firstKey) loadIssueTypes(firstKey)
    } catch (e) {
      setCreateError(e instanceof Error ? e.message : '프로젝트 로드 실패')
      setCreateState('error')
    }
  }

  async function loadIssueTypes(projectKey: string) {
    setCreateState('loading-types')
    try {
      const types = await fetchJiraIssueTypes(projectKey)
      setIssueTypes(types)
      setSelectedType(types[0]?.id ?? '')
      setCreateState('ready')
    } catch (e) {
      setCreateError(e instanceof Error ? e.message : '이슈 유형 로드 실패')
      setCreateState('error')
    }
  }

  function handleProjectChange(key: string) {
    setSelectedProject(key)
    loadIssueTypes(key)
  }

  async function handleCreate() {
    if (!data || !selectedProject || !selectedType) return
    setCreateState('creating')
    setCreateError('')
    try {
      const res = await createJiraTicket({
        projectKey:         selectedProject,
        issueTypeId:        selectedType,
        summary:            data.summary,
        description:        data.description ?? {},
        acceptanceCriteria: data.acceptance_criteria ?? [],
        specContent,
      })
      setResult(res)
      setCreateState('done')
    } catch (e) {
      setCreateError(e instanceof Error ? e.message : '티켓 생성 실패')
      setCreateState('error')
    }
  }

  const isLoading = createState === 'loading-projects' || createState === 'loading-types' || createState === 'creating'
  const formVisible = createState !== 'idle' && createState !== 'done'

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
          <span className="jira-type-badge">
            {status?.defaultIssueTypeName || 'Story'}
          </span>
          <h2 className="jira-summary">{data.summary}</h2>
          <div className="jira-header-actions">
            {createState === 'done' && result ? (
              <a className="jira-created-link" href={result.url} target="_blank" rel="noreferrer">
                {result.key} ↗
              </a>
            ) : hasDefaults ? (
              // 원클릭 생성 (기본값 있음)
              <button
                className="btn-jira-create"
                onClick={handleQuickCreate}
                disabled={isLoading}
                title={`${status!.defaultProjectKey} / ${status!.defaultIssueTypeName}`}
              >
                {createState === 'creating' ? '생성 중...' : `Jira 생성 · ${status!.defaultProjectKey}`}
              </button>
            ) : (
              // 폼 열기 (기본값 없음)
              <button
                className="btn-jira-create"
                onClick={handleOpenForm}
                disabled={isLoading}
              >
                {createState === 'loading-projects' ? '로딩 중...' : 'Jira 생성'}
              </button>
            )}
            <button className="btn-jira-toggle" onClick={() => setShowRaw(true)}>JSON</button>
          </div>
        </div>

        {/* 기본값 생성 예정 미리보기 */}
        {hasDefaults && createState !== 'done' && (
          <div className="jira-create-preview">
            <span className="jira-preview-label">생성 예정</span>
            <span className="jira-preview-chip">{status!.defaultProjectKey}</span>
            <span className="jira-preview-sep">·</span>
            <span className="jira-preview-chip">{status!.defaultIssueTypeName}</span>
            {specContent && (
              <>
                <span className="jira-preview-sep">·</span>
                <span className="jira-preview-attach">spec.md 첨부</span>
              </>
            )}
            {createError && <span className="jira-preview-error">{createError}</span>}
          </div>
        )}

        {/* 에러 표시 (기본값 없을 때) */}
        {!hasDefaults && createError && (
          <div className="jira-create-form">
            <div className="jira-create-error">{createError}</div>
          </div>
        )}

        {/* 드롭다운 폼 (기본값 없을 때만) */}
        {formVisible && !hasDefaults && (
          <div className="jira-create-form">
            {(createState === 'ready' || createState === 'loading-types' || createState === 'creating') && (
              <>
                <div className="jira-create-row">
                  <label className="jira-create-label">프로젝트</label>
                  <select
                    className="jira-create-select"
                    value={selectedProject}
                    onChange={e => handleProjectChange(e.target.value)}
                    disabled={isLoading}
                  >
                    {projects.map(p => (
                      <option key={p.key} value={p.key}>{p.key} — {p.name}</option>
                    ))}
                  </select>
                </div>
                <div className="jira-create-row">
                  <label className="jira-create-label">이슈 유형</label>
                  <select
                    className="jira-create-select"
                    value={selectedType}
                    onChange={e => setSelectedType(e.target.value)}
                    disabled={isLoading || createState === 'loading-types'}
                  >
                    {createState === 'loading-types' && <option>로딩 중...</option>}
                    {issueTypes.map(t => (
                      <option key={t.id} value={t.id}>{t.name}</option>
                    ))}
                  </select>
                </div>
                {specContent && (
                  <div className="jira-create-note">spec.md 첨부 포함</div>
                )}
                <div className="jira-create-actions">
                  <button
                    className="btn-jira-cancel"
                    onClick={() => { setCreateState('idle'); setCreateError('') }}
                    disabled={isLoading}
                  >
                    취소
                  </button>
                  <button
                    className="btn-jira-submit"
                    onClick={handleCreate}
                    disabled={isLoading || !selectedProject || !selectedType}
                  >
                    {createState === 'creating' ? '생성 중...' : 'Jira에 생성'}
                  </button>
                </div>
              </>
            )}
          </div>
        )}

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
