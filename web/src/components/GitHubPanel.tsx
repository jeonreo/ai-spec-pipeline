import { useState, useEffect } from 'react'
import { fetchGithubStatus, fetchSettings, saveSettings, GitHubRepoStatus } from '../api'

export default function GitHubPanel() {
  const [frontendUrl, setFrontendUrl] = useState('')
  const [backendUrl,  setBackendUrl]  = useState('')
  const [status,      setStatus]      = useState<GitHubRepoStatus[]>([])
  const [saving,      setSaving]      = useState(false)
  const [checking,    setChecking]    = useState(false)

  useEffect(() => {
    fetchSettings().then(s => {
      setFrontendUrl(s.github?.frontendRepoUrl ?? '')
      setBackendUrl(s.github?.backendRepoUrl   ?? '')
    }).catch(() => {})
  }, [])

  async function handleSave() {
    setSaving(true)
    try {
      const current = await fetchSettings()
      await saveSettings({
        ...current,
        github: { frontendRepoUrl: frontendUrl.trim(), backendRepoUrl: backendUrl.trim() },
      })
      await checkStatus()
    } catch (e) {
      alert(e instanceof Error ? e.message : '저장 실패')
    } finally {
      setSaving(false)
    }
  }

  async function checkStatus() {
    setChecking(true)
    try { setStatus(await fetchGithubStatus()) }
    catch { setStatus([]) }
    finally { setChecking(false) }
  }

  const statusMap = Object.fromEntries(status.map(s => [s.label, s]))

  return (
    <div className="github-panel">
      <div className="panel-section-header">
        <span>GitHub 저장소</span>
        <button className="btn-text" onClick={checkStatus} disabled={checking}>
          {checking ? '확인 중...' : '연결 확인'}
        </button>
      </div>

      {(['frontend', 'backend'] as const).map(label => {
        const url = label === 'frontend' ? frontendUrl : backendUrl
        const setUrl = label === 'frontend' ? setFrontendUrl : setBackendUrl
        const s = statusMap[label]
        return (
          <div key={label} className="github-repo-group">
            <div className="github-repo-row">
              <label className="github-repo-label">{label === 'frontend' ? 'FE' : 'BE'}</label>
              <input
                className={`github-repo-input${s && !s.connected ? ' github-repo-input--error' : ''}`}
                placeholder={`https://github.com/owner/${label}-repo`}
                value={url}
                onChange={e => { setUrl(e.target.value); }}
              />
              {s && (
                s.connected
                  ? <span className="repo-status repo-status--ok">✓</span>
                  : <span className="repo-status repo-status--err">✗</span>
              )}
            </div>
            {s?.connected && (
              <div className="repo-status-ok-msg">
                {s.repoName} · default: {s.defaultBranch}
              </div>
            )}
            {s && !s.connected && s.error && (
              <div className="repo-status-err-msg">{s.error}</div>
            )}
          </div>
        )
      })}

      <div className="github-panel-footer">
        <span className="github-panel-note">Code Agent가 FE·BE 동시 검색</span>
        <button className="btn-save-github" onClick={handleSave} disabled={saving}>
          {saving ? '저장 중...' : '저장'}
        </button>
      </div>
    </div>
  )
}
