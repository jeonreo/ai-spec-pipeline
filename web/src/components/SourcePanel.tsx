import { useState, useEffect, useRef } from 'react'
import { Tab, RunState } from '../App'
import ProjectKnowledgePanel from './ProjectKnowledgePanel'
import GitHubPanel from './GitHubPanel'
import { fetchJiraProjects, fetchJiraIssueTypes, fetchJiraStatus, JiraProject, JiraIssueType } from '../api'

interface Props {
  input: string
  onInputChange: (v: string) => void
  onRun: (tab: Tab) => void
  runStates: Record<Tab, RunState>
  stale: Record<Tab, boolean>
  jiraProjectKey: string
  jiraIssueTypeName: string
  onJiraConfigChange: (key: string, type: string) => void
  projectKnowledge: string
  onProjectKnowledgeChange: (v: string) => void
  decisions: string
  intakeOutput: string
}

export default function SourcePanel({
  input, onInputChange, onRun, runStates, stale,
  jiraProjectKey, jiraIssueTypeName, onJiraConfigChange,
  projectKnowledge, onProjectKnowledgeChange, decisions, intakeOutput,
}: Props) {
  const intakeState   = runStates.intake
  const intakeDone    = intakeState === 'done'
  const intakeRunning = intakeState === 'running'

  const [projects, setProjects]       = useState<JiraProject[]>([])
  const [issueTypes, setIssueTypes]   = useState<JiraIssueType[]>([])
  const [jiraLoading, setJiraLoading] = useState(false)
  const [typesLoading, setTypesLoading] = useState(false)

  const defaultIssueTypeName = useRef('')

  // Load projects + status on mount, waiting for backend to be ready
  useEffect(() => {
    let cancelled = false

    async function waitAndLoad() {
      setJiraLoading(true)
      // 첫 시도 전 1초 대기 (서버 기동 시간 확보)
      await new Promise(r => setTimeout(r, 1000))
      // Poll until backend responds (max 30s, 1s interval)
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
          // backend not ready yet — wait and retry
          await new Promise(r => setTimeout(r, 1000))
        }
      }
      // Timed out
      setJiraLoading(false)
    }

    waitAndLoad()
    return () => { cancelled = true }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  // Load issue types when project changes (with retry until backend ready)
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

  return (
    <>
      {/* Sources */}
      <div className="sidebar-section">
        <div className="sidebar-section-header">
          <span className="sidebar-section-icon">📄</span>
          <span className="sidebar-section-title">Sources</span>
        </div>
        <textarea
          className="sidebar-textarea"
          value={input}
          onChange={e => onInputChange(e.target.value)}
          placeholder="요구사항, 슬랙 스레드, 미팅 노트 등을 자유롭게 입력하세요."
        />
        <button
          className={`btn-sidebar-run${intakeRunning ? ' btn-sidebar-run--running' : ''}${intakeDone ? ' btn-sidebar-run--done' : ''}${intakeState === 'failed' ? ' btn-sidebar-run--failed' : ''}`}
          onClick={() => onRun('intake')}
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
