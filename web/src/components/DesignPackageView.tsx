import { useMemo, useState } from 'react'
import {
  buildDesignPreviewHtml,
  getFigmaMakePrompt,
  isLegacyDesignHtml,
  parseDesignPackage,
} from '../designPackage'

interface Props {
  content: string
  onChange: (value: string) => void
}

export default function DesignPackageView({ content, onChange }: Props) {
  const [copyStatus, setCopyStatus] = useState('')
  const parsed = useMemo(() => parseDesignPackage(content), [content])
  const legacyHtml = !parsed && isLegacyDesignHtml(content)
  const figmaMakePrompt = parsed ? getFigmaMakePrompt(parsed) : ''

  async function handleCopy(label: string, value: string) {
    try {
      await navigator.clipboard.writeText(value)
      setCopyStatus(`${label} copied`)
      window.setTimeout(() => setCopyStatus(''), 1800)
    } catch {
      setCopyStatus(`Failed to copy ${label.toLowerCase()}`)
      window.setTimeout(() => setCopyStatus(''), 1800)
    }
  }

  function openPreview() {
    const html = parsed ? buildDesignPreviewHtml(parsed) : content
    const blob = new Blob([html], { type: 'text/html' })
    window.open(URL.createObjectURL(blob), '_blank', 'noopener,noreferrer')
  }

  return (
    <div className="design-package">
      <div className="design-package-toolbar">
        <button
          className="btn-preview"
          onClick={openPreview}
          disabled={!parsed && !legacyHtml}
        >
          Open Quick Preview
        </button>
        <button
          className="btn-preview"
          onClick={() => handleCopy('Figma Make prompt', figmaMakePrompt)}
          disabled={!parsed || !figmaMakePrompt}
        >
          Copy for Figma Make
        </button>
        {copyStatus && <span className="design-copy-status">{copyStatus}</span>}
      </div>

      {!parsed && !legacyHtml && content.trim() && (
        <div className="warning-banner">
          Design output is not valid Design Package JSON yet. Preview is disabled until the JSON parses correctly.
        </div>
      )}

      {legacyHtml && (
        <div className="warning-banner">
          Legacy HTML design output detected. You can still open the preview, but new runs will generate Design Package JSON.
        </div>
      )}

      {parsed && (
        <div className="design-package-summary">
          <div className="design-package-card">
            <span className="design-package-label">Screen</span>
            <strong>{parsed.meta?.screenName || parsed.meta?.screenId || 'Untitled screen'}</strong>
            <p>{parsed.purpose?.summary || 'No summary provided.'}</p>
          </div>

          <div className="design-package-grid">
            <section className="design-package-card">
              <span className="design-package-label">Layout</span>
              <strong>{parsed.layout?.pattern || 'Not specified'}</strong>
              <div className="design-chip-list">
                {(parsed.layout?.structure ?? []).map(item => (
                  <span key={item} className="design-chip">{item}</span>
                ))}
              </div>
            </section>

            <section className="design-package-card">
              <span className="design-package-label">Components</span>
              <div className="design-chip-list">
                {(parsed.components?.recommended ?? []).map(item => (
                  <span key={item} className="design-chip">{item}</span>
                ))}
              </div>
            </section>
          </div>
        </div>
      )}

      <details className="design-advanced">
        <summary>Advanced</summary>
        <div className="design-advanced-body">
          {parsed && (
            <div className="design-advanced-actions">
              <button
                className="btn-preview"
                onClick={() => handleCopy('Design Package', content)}
              >
                Copy JSON
              </button>
            </div>
          )}
          <textarea
            className="output-textarea"
            value={content}
            onChange={event => onChange(event.target.value)}
          />
        </div>
      </details>
    </div>
  )
}
