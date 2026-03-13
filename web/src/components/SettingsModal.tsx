import { useState, useEffect } from 'react'
import { fetchSettings, saveSettings, type PipelineSettings } from '../api'

const STAGES = ['intake', 'spec', 'jira', 'qa', 'design', 'code-analysis', 'patch'] as const

const STAGE_LABELS: Record<string, string> = {
  intake:          'Intake',
  spec:            'Spec',
  jira:            'Jira',
  qa:              'QA',
  design:          'Design',
  'code-analysis': 'Code Analysis',
  patch:           'Patch',
}

const CLI_MODELS = [
  { id: 'claude-haiku-4-5-20251001', label: 'Haiku 4.5 (빠름 · 저렴)' },
  { id: 'claude-sonnet-4-6',         label: 'Sonnet 4.6 (균형)' },
  { id: 'claude-opus-4-6',           label: 'Opus 4.6 (최고 품질)' },
]

interface Props {
  onClose: () => void
  isVertex: boolean
}

export default function SettingsModal({ onClose, isVertex }: Props) {
  const [settings, setSettings] = useState<PipelineSettings | null>(null)
  const [saving, setSaving]     = useState(false)
  const [saved, setSaved]       = useState(false)
  const [error, setError]       = useState('')

  useEffect(() => {
    fetchSettings().then(setSettings).catch(() => setError('설정을 불러오지 못했습니다.'))
  }, [])

  function handleModelChange(stage: string, model: string) {
    if (!settings) return
    setSettings({ ...settings, stageModels: { ...settings.stageModels, [stage]: model } })
    setSaved(false)
  }

  async function handleSave() {
    if (!settings) return
    setSaving(true)
    setError('')
    try {
      await saveSettings(settings)
      setSaved(true)
    } catch {
      setError('저장에 실패했습니다.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="policy-overlay" onClick={onClose}>
      <div className="policy-modal settings-modal" onClick={e => e.stopPropagation()}>
        <div className="policy-modal-header">
          <span>모델 설정</span>
          <button onClick={onClose}>✕</button>
        </div>

        <div className="settings-modal-body">
          {isVertex ? (
            <div className="settings-vertex-info">
              <p className="settings-desc">Vertex AI 연결 중 — 모든 스테이지에서 <strong>claude-sonnet-4-6</strong> 고정 사용</p>
            </div>
          ) : !settings ? (
            <p className="settings-loading">{error || '불러오는 중…'}</p>
          ) : (
            <>
              <p className="settings-desc">로컬 CLI 실행 시 각 스테이지에서 사용할 모델을 선택하세요.</p>
              <table className="settings-table">
                <thead>
                  <tr>
                    <th>Stage</th>
                    <th>Model</th>
                  </tr>
                </thead>
                <tbody>
                  {STAGES.map(stage => (
                    <tr key={stage}>
                      <td className="settings-stage-name">{STAGE_LABELS[stage]}</td>
                      <td>
                        <select
                          value={settings.stageModels[stage] ?? CLI_MODELS[0].id}
                          onChange={e => handleModelChange(stage, e.target.value)}
                          className="settings-model-select"
                        >
                          {CLI_MODELS.map(m => (
                            <option key={m.id} value={m.id}>{m.label}</option>
                          ))}
                        </select>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {error && <p className="settings-error">{error}</p>}

              <div className="settings-footer">
                {saved && <span className="settings-saved">저장됨</span>}
                <button className="btn-policy" onClick={handleSave} disabled={saving}>
                  {saving ? '저장 중…' : '저장'}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
