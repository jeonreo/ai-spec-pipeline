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
  initialProjectKey?: string
  initialIssueTypeName?: string
  onCreated?: (key: string) => void
}

type FormState = 'loading' | 'ready' | 'loading-types' | 'creating' | 'done' | 'error'

function parseJiraData(content: string): JiraData | null {
  if (!content) return null

  try {
    const parsed = JSON.parse(content) as Record<string, unknown>
    const summary = typeof parsed.summary === 'string' ? parsed.summary : ''
    const description = parsed.description && typeof parsed.description === 'object' && !Array.isArray(parsed.description)
      ? Object.fromEntries(
          Object.entries(parsed.description as Record<string, unknown>)
            .filter(([, value]) => typeof value === 'string')
            .map(([key, value]) => [key, value as string]),
        )
      : {}

    const rawCriteria = Array.isArray(parsed.acceptance_criteria)
      ? parsed.acceptance_criteria
      : Array.isArray(parsed.acceptanceCriteria)
        ? parsed.acceptanceCriteria
        : []

    return {
      summary,
      description,
      acceptance_criteria: rawCriteria.filter((item): item is string => typeof item === 'string'),
    }
  } catch {
    return null
  }
}

export default function JiraView({ content, onChange, specContent, initialProjectKey, initialIssueTypeName, onCreated }: Props) {
  const [showRaw, setShowRaw] = useState(false)
  const [status, setStatus] = useState<JiraStatus | null>(null)
  const [formState, setFormState] = useState<FormState>('loading')
  const [projects, setProjects] = useState<JiraProject[]>([])
  const [issueTypes, setIssueTypes] = useState<JiraIssueType[]>([])
  const [selectedProject, setSelectedProject] = useState('')
  const [selectedType, setSelectedType] = useState('')
  const [createError, setCreateError] = useState('')
  const [result, setResult] = useState<CreateJiraResult | null>(null)

  // 마운트 시 status → 프로젝트 → 이슈유형 순서로 로드, 기본값으로 pre-select
  useEffect(() => {
    async function init() {
      // 1. status (항상 성공)
      let s: JiraStatus
      try {
        s = await fetchJiraStatus()
        setStatus(s)
      } catch {
        setCreateError('Jira 상태 로드 실패')
        setFormState('error')
        return
      }

      // 2. 프로젝트 목록
      let list: JiraProject[]
      try {
        list = await fetchJiraProjects()
        setProjects(list)
      } catch (e) {
        setCreateError(e instanceof Error ? e.message : '프로젝트 로드 실패')
        setFormState('error')
        return
      }

      // 3. 기본 프로젝트 선택 (사이드바 설정 → appsettings 기본값 순서로 우선)
      const preferredKey = initialProjectKey || s.defaultProjectKey
      const projectKey = list.find(p => p.key === preferredKey)?.key ?? list[0]?.key ?? ''
      setSelectedProject(projectKey)

      // 4. 이슈 유형 로드 + 기본 유형 선택
      if (projectKey) {
        setFormState('loading-types')
        try {
          const types = await fetchJiraIssueTypes(projectKey)
          setIssueTypes(types)
          const preferredType = initialIssueTypeName || s.defaultIssueTypeName
          const defaultType = types.find(
            t => t.name.toLowerCase() === preferredType?.toLowerCase()
          ) ?? types[0]
          setSelectedType(defaultType?.id ?? '')
        } catch (e) {
          setCreateError(e instanceof Error ? e.message : '이슈 유형 로드 실패')
          setFormState('error')
          return
        }
      }
      setFormState('ready')
    }
    init()
  }, [])

  async function loadIssueTypes(projectKey: string) {
    setFormState('loading-types')
    setIssueTypes([])
    setSelectedType('')
    try {
      const types = await fetchJiraIssueTypes(projectKey)
      setIssueTypes(types)
      setSelectedType(types[0]?.id ?? '')
      setFormState('ready')
    } catch (e) {
      setCreateError(e instanceof Error ? e.message : '이슈 유형 로드 실패')
      setFormState('error')
    }
  }

  function handleProjectChange(key: string) {
    setSelectedProject(key)
    loadIssueTypes(key)
  }

  async function handleCreate() {
    if (!data || !selectedProject || !selectedType) return
    setFormState('creating')
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
      setFormState('done')
      onCreated?.(res.key)
    } catch (e) {
      setCreateError(e instanceof Error ? e.message : '티켓 생성 실패')
      setFormState('error')
    }
  }

  const data = parseJiraData(content)

  const isLoading = formState === 'loading' || formState === 'loading-types' || formState === 'creating'
  const selectedTypeName = issueTypes.find(t => t.id === selectedType)?.name ?? status?.defaultIssueTypeName ?? 'Story'

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
          <span className="jira-type-badge">{selectedTypeName}</span>
          <h2 className="jira-summary">{data.summary}</h2>
          <div className="jira-header-actions">
            {formState === 'done' && result ? (
              <a className="jira-created-link" href={result.url} target="_blank" rel="noreferrer">
                {result.key} ↗
              </a>
            ) : (
              <button className="btn-jira-toggle" onClick={() => setShowRaw(true)}>JSON</button>
            )}
          </div>
        </div>

        {/* 생성 폼 — 항상 표시 */}
        {formState !== 'done' && (
          <div className="jira-create-form">
            <div className="jira-create-row">
              <label className="jira-create-label">프로젝트</label>
              <select
                className="jira-create-select"
                value={selectedProject}
                onChange={e => handleProjectChange(e.target.value)}
                disabled={isLoading}
              >
                {formState === 'loading' && <option value="">로딩 중...</option>}
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
                disabled={isLoading}
              >
                {formState === 'loading-types' && <option value="">로딩 중...</option>}
                {issueTypes.map(t => (
                  <option key={t.id} value={t.id}>{t.name}</option>
                ))}
              </select>
            </div>
            <div className="jira-create-actions">
              <span className={specContent ? 'jira-preview-chip jira-preview-chip--spec' : 'jira-preview-chip jira-preview-chip--spec-empty'}>
                {specContent ? 'spec.md 첨부' : 'spec.md 없음'}
              </span>
              {createError && <span className="jira-preview-error">{createError}</span>}
              <button
                className="btn-jira-submit"
                onClick={handleCreate}
                disabled={isLoading || !selectedProject || !selectedType}
              >
                {formState === 'creating' ? '생성 중...' : 'Jira에 생성'}
              </button>
            </div>
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
