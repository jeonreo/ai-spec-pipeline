import { useState, useEffect, useRef } from 'react'
import { Tab, RunState } from '../App'
import ProjectKnowledgePanel from './ProjectKnowledgePanel'
import GitHubPanel from './GitHubPanel'
import { fetchJiraProjects, fetchJiraIssueTypes, fetchJiraStatus, JiraProject, JiraIssueType, extractFromSlack, fetchSlackStatus } from '../api'
import { slackStore } from '../slackStore'

interface Props {
  input: string
  onInputChange: (v: string) => void
  onRun: (tab: Tab) => void
  onRunWithFiles?: (tab: Tab, files: File[]) => void
  runStates: Record<Tab, RunState>
  stale: Record<Tab, boolean>
  jiraProjectKey: string
  jiraIssueTypeName: string
  onJiraConfigChange: (key: string, type: string) => void
  projectKnowledge: string
  onProjectKnowledgeChange: (v: string) => void
  decisions: string
  intakeOutput: string
  isVertex?: boolean
}

type InputMode = 'text' | 'slack'

export default function SourcePanel({
  input, onInputChange, onRun, onRunWithFiles, runStates, stale,
  jiraProjectKey, jiraIssueTypeName, onJiraConfigChange,
  projectKnowledge, onProjectKnowledgeChange, decisions, intakeOutput, isVertex,
}: Props) {
  const intakeState   = runStates.intake
  const intakeDone    = intakeState === 'done'
  const intakeRunning = intakeState === 'running'

  const [projects, setProjects]         = useState<JiraProject[]>([])
  const [issueTypes, setIssueTypes]     = useState<JiraIssueType[]>([])
  const [jiraLoading, setJiraLoading]   = useState(false)
  const [typesLoading, setTypesLoading] = useState(false)

  // 파일 첨부
  const [attachedFiles, setAttachedFiles] = useState<File[]>([])
  const fileInputRef = useRef<HTMLInputElement>(null)

  // Slack URL 모드
  const [inputMode, setInputMode]         = useState<InputMode>('text')
  const [slackUrl, setSlackUrl]           = useState('')
  const [slackExtracting, setSlackExtracting] = useState(false)
  const [slackError, setSlackError]       = useState('')
  const [slackConfigured, setSlackConfigured] = useState(false)
  const [slackFileCount, setSlackFileCount]   = useState(slackStore.count())

  const defaultIssueTypeName = useRef('')

  // Slack 설정 여부 확인
  useEffect(() => {
    fetchSlackStatus().then(s => setSlackConfigured(s.configured)).catch(() => {})
  }, [])

  // Load projects + status on mount, waiting for backend to be ready
  useEffect(() => {
    let cancelled = false

    async function waitAndLoad() {
      setJiraLoading(true)
      await new Promise(r => setTimeout(r, 1000))
      for (let i = 0; i < 30; i++) {
        if (cancelled) return
        try {
          const [status, list] = await Promise.all([fetchJiraStatus(), fetchJiraProjects()])
          if (cancelled) return
          defaultIssueTypeName.current = status.defaultIssueTypeName
          setProjects(list)
          if (!jiraProjectKey) {
            const key = list.find(p => p.key === status.defaultProjectKey)?.key ?? list[0]?.key ?? ''
            if (key) onJiraConfigChange(key, jiraIssueTypeName)
          }
          setJiraLoading(false)
          return
        } catch {
          await new Promise(r => setTimeout(r, 1000))
        }
      }
      setJiraLoading(false)
    }

    waitAndLoad()
    return () => { cancelled = true }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  // Load issue types when project changes
  useEffect(() => {
    if (!jiraProjectKey) return
    let cancelled = false
    async function loadTypes() {
      setTypesLoading(true)
      for (let i = 0; i < 30; i++) {
        if (cancelled) return
        try {
          const types = await fetchJiraIssueTypes(jiraProjectKey)
          if (cancelled) return
          setIssueTypes(types)
          if (!jiraIssueTypeName || !types.find(t => t.name === jiraIssueTypeName)) {
            const preferred = defaultIssueTypeName.current
            const type = types.find(t => t.name.toLowerCase() === preferred.toLowerCase()) ?? types[0]
            if (type) onJiraConfigChange(jiraProjectKey, type.name)
          }
          setTypesLoading(false)
          return
        } catch {
          await new Promise(r => setTimeout(r, 1000))
        }
      }
      setIssueTypes([])
      setTypesLoading(false)
    }
    loadTypes()
    return () => { cancelled = true }
  }, [jiraProjectKey]) // eslint-disable-line react-hooks/exhaustive-deps

  function handleProjectChange(key: string) {
    onJiraConfigChange(key, '')
  }

  function handleFileSelect(e: React.ChangeEvent<HTMLInputElement>) {
    const newFiles = Array.from(e.target.files ?? [])
    const MAX_SIZE = 5 * 1024 * 1024 // 5MB
    const valid = newFiles.filter(f => {
      if (f.size > MAX_SIZE) { alert(`${f.name}: 5MB 초과 파일은 첨부할 수 없습니다.`); return false }
      return true
    })
    setAttachedFiles(prev => [...prev, ...valid])
    // reset input so same file can be re-selected
    if (fileInputRef.current) fileInputRef.current.value = ''
  }

  function removeFile(idx: number) {
    setAttachedFiles(prev => prev.filter((_, i) => i !== idx))
  }

  async function handleSlackExtract() {
    if (!slackUrl.trim()) return
    setSlackExtracting(true)
    setSlackError('')
    try {
      const result = await extractFromSlack(slackUrl.trim())
      onInputChange(result.text)
      slackStore.set(result.files)
      setSlackFileCount(result.files.length)
      setInputMode('text') // 텍스트 탭으로 전환하여 추출된 내용 확인
    } catch (e) {
      setSlackError(e instanceof Error ? e.message : '추출 실패')
    } finally {
      setSlackExtracting(false)
    }
  }

  function handleRunIntake() {
    if (attachedFiles.length > 0 && onRunWithFiles) {
      onRunWithFiles('intake', attachedFiles)
    } else {
      onRun('intake')
    }
  }

  return (
    <>
      {/* Sources */}
      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <span className="sidebar-section-icon">📄</span>
          <span className="sidebar-section-title">Sources</span>
          <div className="source-mode-tabs">
            <button
              className={`source-mode-tab${inputMode === 'text' ? ' source-mode-tab--active' : ''}`}
              onClick={() => setInputMode('text')}
            >
              직접 입력
            </button>
            <button
              className={`source-mode-tab${inputMode === 'slack' ? ' source-mode-tab--active' : ''}`}
              onClick={() => setInputMode('slack')}
            >
              Slack 링크
            </button>
          </div>
        </div>

        {inputMode === 'text' ? (
          <>
            <textarea
              className="sidebar-textarea"
              value={input}
              onChange={e => onInputChange(e.target.value)}
              placeholder="요구사항, 슬랙 스레드, 미팅 노트 등을 자유롭게 입력하세요."
            />

            {/* 파일 첨부 */}
            <div className="file-attach-area">
              <input
                ref={fileInputRef}
                type="file"
                accept="image/png,image/jpeg,image/webp,image/gif"
                multiple
                style={{ display: 'none' }}
                onChange={handleFileSelect}
              />
              <button
                className="btn-attach-file"
                onClick={() => fileInputRef.current?.click()}
                title="이미지 첨부 (PNG, JPG, WebP, GIF · 최대 5MB)"
              >
                📎 이미지 첨부
              </button>
              {!isVertex && attachedFiles.length > 0 && (
                <span className="attach-vertex-note">Vertex AI 모드에서만 이미지 분석 가능</span>
              )}
            </div>

            {attachedFiles.length > 0 && (
              <div className="file-preview-list">
                {attachedFiles.map((f, i) => (
                  <div key={i} className="file-preview-item">
                    <img
                      src={URL.createObjectURL(f)}
                      alt={f.name}
                      className="file-preview-thumb"
                    />
                    <span className="file-preview-name" title={f.name}>
                      {f.name.length > 20 ? f.name.slice(0, 18) + '…' : f.name}
                    </span>
                    <button className="file-preview-remove" onClick={() => removeFile(i)}>✕</button>
                  </div>
                ))}
              </div>
            )}

            {slackFileCount > 0 && (
              <div className="slack-files-badge">
                Slack 첨부파일 {slackFileCount}개 수집됨 — Jira 생성 시 자동 첨부
              </div>
            )}
          </>
        ) : (
          /* Slack URL 입력 모드 */
          <div className="slack-extract-panel">
            {!slackConfigured && (
              <div className="slack-unconfigured-note">
                Slack Bot Token 미설정 — .env에 SLACK__BOTTOKEN 추가 필요
              </div>
            )}
            <input
              className="slack-url-input"
              type="url"
              value={slackUrl}
              onChange={e => setSlackUrl(e.target.value)}
              placeholder="https://yourworkspace.slack.com/archives/C.../p..."
              disabled={slackExtracting}
            />
            <button
              className="btn-slack-extract"
              onClick={handleSlackExtract}
              disabled={slackExtracting || !slackUrl.trim() || !slackConfigured}
            >
              {slackExtracting ? '추출 중...' : '본문 + 스레드 추출'}
            </button>
            {slackError && <div className="slack-extract-error">{slackError}</div>}
            <div className="slack-extract-hint">
              추출 후 "직접 입력" 탭에서 내용을 확인할 수 있습니다.
              이미지 첨부파일은 Jira 생성 시 자동으로 등록됩니다.
            </div>
          </div>
        )}

        <button
          className={`btn-sidebar-run${intakeRunning ? ' btn-sidebar-run--running' : ''}${intakeDone ? ' btn-sidebar-run--done' : ''}${intakeState === 'failed' ? ' btn-sidebar-run--failed' : ''}`}
          onClick={handleRunIntake}
          disabled={intakeRunning}
        >
          {intakeRunning
            ? <><span className="sidebar-run-dots"><span /><span /><span /></span>Intake 실행 중</>
            : intakeDone
            ? '↺ Regenerate Intake'
            : '▶ Generate Intake'}
          {intakeDone && stale.intake && <span className="sidebar-stale-badge">stale</span>}
        </button>
      </div>

      {/* Jira 설정 */}
      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <span className="sidebar-section-icon">🎯</span>
          <span className="sidebar-section-title">Jira 설정</span>
        </div>
        <div className="jira-settings-body">
          <div className="jira-settings-row">
            <label className="jira-settings-label">Project</label>
            <select
              className="jira-settings-select"
              value={jiraProjectKey}
              onChange={e => handleProjectChange(e.target.value)}
              disabled={jiraLoading}
            >
              {jiraLoading && <option value="">Loading...</option>}
              {!jiraLoading && projects.length === 0 && <option value="">No projects</option>}
              {projects.map(p => (
                <option key={p.key} value={p.key}>{p.key} — {p.name}</option>
              ))}
            </select>
          </div>
          <div className="jira-settings-row">
            <label className="jira-settings-label">Issue Type</label>
            <select
              className="jira-settings-select"
              value={jiraIssueTypeName}
              onChange={e => onJiraConfigChange(jiraProjectKey, e.target.value)}
              disabled={typesLoading || !jiraProjectKey}
            >
              {typesLoading && <option value="">Loading...</option>}
              {!typesLoading && issueTypes.length === 0 && <option value="">—</option>}
              {issueTypes.map(t => (
                <option key={t.id} value={t.name}>{t.name}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* GitHub 저장소 설정 */}
      <GitHubPanel />

      {/* 프로젝트 지식 */}
      <ProjectKnowledgePanel
        knowledge={projectKnowledge}
        decisions={decisions}
        intakeOutput={intakeOutput}
        onKnowledgeChange={onProjectKnowledgeChange}
      />
    </>
  )
}
