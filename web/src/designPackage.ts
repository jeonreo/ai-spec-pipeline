import cloverPreviewCss from './assets/cloverPreview.css?raw'

export interface CloverField {
  label?: string
  value?: string
  badge?: string
}

export interface CloverMetaItem {
  label?: string
  value?: string
  badge?: string
}

export interface CloverCard {
  title?: string
  subtitle?: string
  status?: string
  meta?: CloverMetaItem[]
  bullets?: string[]
  actions?: string[]
}

export interface CloverSummaryItem {
  label?: string
  value?: string
  subValue?: string
}

export interface CloverToolbar {
  primarySearchPlaceholder?: string
  secondarySearchPlaceholder?: string
  filterChips?: string[]
  resultLabel?: string
}

export interface CloverLeftPanel {
  title?: string
  subtitle?: string
  badges?: string[]
  fields?: CloverField[]
  actions?: string[]
}

export interface CloverTable {
  columns?: string[]
  rows?: string[][]
}

export interface CloverAdapterHints {
  layoutPattern?: string
  menuGroup?: string
  menuItems?: string[]
  breadcrumbs?: string[]
  toolbar?: CloverToolbar
  leftPanel?: CloverLeftPanel
  summaryGrid?: CloverSummaryItem[]
  cards?: CloverCard[]
  table?: CloverTable
  badgeMapping?: Record<string, string>
  preferredBlocks?: string[]
}

export interface DesignPackage {
  version?: string
  meta?: {
    screenId?: string
    screenName?: string
    screenType?: string
    sourceSpecVersion?: string
    designSystemTarget?: string
    locale?: string
  }
  purpose?: {
    summary?: string
    primaryUser?: string
    businessGoal?: string
    successCriteria?: string[]
  }
  layout?: {
    pattern?: string
    structure?: string[]
    density?: string
    priority?: string[]
  }
  sections?: Array<{
    id?: string
    title?: string
    role?: string
    description?: string
    contents?: string[]
  }>
  dataModel?: {
    entities?: string[]
    keyFields?: string[]
    tableColumns?: string[]
  }
  components?: {
    recommended?: string[]
    requiredBehaviors?: string[]
    forbiddenPatterns?: string[]
  }
  states?: {
    screenStates?: string[]
    componentStates?: string[]
    statusLabels?: string[]
  }
  interactions?: {
    rules?: string[]
    accessibility?: string[]
  }
  styleNotes?: {
    tone?: string
    visualPriority?: string[]
    notes?: string[]
  }
  handoff?: {
    figmaMakePrompt?: string
    implementationNotes?: string[]
  }
  adapterHints?: {
    clover?: CloverAdapterHints
  }
}

function stripCodeFence(content: string): string {
  const trimmed = content.trim()
  if (!trimmed.startsWith('```')) return trimmed

  const firstNewline = trimmed.indexOf('\n')
  const withoutHeader = firstNewline >= 0 ? trimmed.slice(firstNewline + 1) : trimmed
  const lastFence = withoutHeader.lastIndexOf('```')
  return (lastFence >= 0 ? withoutHeader.slice(0, lastFence) : withoutHeader).trim()
}

export function parseDesignPackage(content: string): DesignPackage | null {
  try {
    const parsed = JSON.parse(stripCodeFence(content))
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null
    return parsed as DesignPackage
  } catch {
    return null
  }
}

export function isLegacyDesignHtml(content: string): boolean {
  return /^\s*(<!doctype html|<html)/i.test(content)
}

export function getFigmaMakePrompt(pkg: DesignPackage): string {
  const explicit = pkg.handoff?.figmaMakePrompt?.trim()
  if (explicit) return explicit

  const screenName = pkg.meta?.screenName?.trim() || 'Admin screen'
  const summary = pkg.purpose?.summary?.trim()
  const sections = (pkg.sections ?? [])
    .map(section => section.title?.trim())
    .filter((value): value is string => Boolean(value))
  const components = (pkg.components?.recommended ?? []).slice(0, 8)

  return [
    `Create a ${screenName} screen using the existing design system only.`,
    summary,
    sections.length > 0 ? `Include sections: ${sections.join(', ')}.` : '',
    components.length > 0 ? `Prefer components: ${components.join(', ')}.` : '',
    'Keep the structure close to the current Clover admin patterns and avoid inventing new layouts.',
  ]
    .filter(Boolean)
    .join(' ')
}

function escapeHtml(value: string): string {
  return value
    .split('&').join('&amp;')
    .split('<').join('&lt;')
    .split('>').join('&gt;')
    .split('"').join('&quot;')
    .split("'").join('&#39;')
}

function normalizeKey(value: string): string {
  return value.trim().toLowerCase()
}

function sampleValueFor(label: string, index = 0): string {
  const normalized = normalizeKey(label)

  if (normalized.includes('email')) return `sample${index + 1}@clo3d.com`
  if (normalized.includes('license')) return index === 0 ? 'CLO NETWORK ONLINEAUTH' : 'CLO TRIAL'
  if (normalized.includes('date') || normalized.includes('time')) return index === 0 ? '2026-03-07' : '2026-03-08'
  if (normalized.includes('count') || normalized.includes('cnt') || normalized.includes('qty')) return String(index + 1)
  if (normalized.includes('status')) return index % 2 === 0 ? 'Active' : 'Integrated'
  if (normalized.includes('country')) return index % 2 === 0 ? 'Korea' : 'Global'
  if (normalized.includes('user') || normalized.includes('member')) return index % 2 === 0 ? 'reoreo' : 'sample-user'
  if (normalized.includes('source')) return index % 2 === 0 ? 'Global / CLO' : 'CLO / Academy'
  if (normalized.includes('api')) return index % 2 === 0 ? 'Off' : 'On'
  if (normalized.includes('software')) return index === 0 ? 'CLO NETWORK ONLINEAUTH' : 'CLO TRIAL'
  if (normalized.includes('type')) return 'Subscription'
  if (normalized.includes('id')) return index === 0 ? 'clover001' : 'clover002'

  return 'Sample value'
}

function inferBreadcrumbs(pkg: DesignPackage, clover?: CloverAdapterHints): string[] {
  if ((clover?.breadcrumbs?.length ?? 0) > 0) return clover!.breadcrumbs!

  const screenName = pkg.meta?.screenName || 'Member'
  const screenType = pkg.meta?.screenType || 'detail'
  return screenType === 'list'
    ? ['Member', screenName]
    : ['Member', 'Member List', screenName]
}

function inferMenuItems(pkg: DesignPackage, clover?: CloverAdapterHints): string[] {
  if ((clover?.menuItems?.length ?? 0) > 0) return clover!.menuItems!

  const sectionItems = (pkg.sections ?? [])
    .map(section => section.title)
    .filter((value): value is string => Boolean(value))
    .slice(0, 4)

  return sectionItems.length > 0 ? sectionItems : ['Headquarter List', 'Member List']
}

function inferToolbar(pkg: DesignPackage, clover?: CloverAdapterHints): CloverToolbar {
  const filterSection = (pkg.sections ?? []).find(section => section.role === 'filter' || section.role === 'form')
  const filterChips = clover?.toolbar?.filterChips
    ?? filterSection?.contents?.slice(0, 6)
    ?? (pkg.states?.statusLabels ?? []).slice(0, 6)

  return {
    primarySearchPlaceholder:
      clover?.toolbar?.primarySearchPlaceholder
      || pkg.dataModel?.keyFields?.[0]
      || 'License ID',
    secondarySearchPlaceholder:
      clover?.toolbar?.secondarySearchPlaceholder
      || pkg.dataModel?.keyFields?.[1]
      || 'Email Address',
    filterChips,
    resultLabel:
      clover?.toolbar?.resultLabel
      || (pkg.meta?.screenType === 'list' ? `Results ${Math.max(3, (pkg.dataModel?.tableColumns?.length ?? 0) + 1)}` : ''),
  }
}

function inferLeftPanel(pkg: DesignPackage, clover?: CloverAdapterHints): CloverLeftPanel {
  if (clover?.leftPanel) {
    return clover.leftPanel
  }

  const statusLabels = pkg.states?.statusLabels ?? []
  const keyFields = pkg.dataModel?.keyFields ?? []
  const title = pkg.meta?.screenName || 'Member Detail'
  const fields = keyFields.slice(0, 8).map((field, index) => ({
    label: field,
    value: sampleValueFor(field, index),
    badge: normalizeKey(field).includes('status') ? (statusLabels[0] || 'Active') : undefined,
  }))

  return {
    title,
    subtitle: pkg.purpose?.summary || pkg.purpose?.businessGoal,
    badges: statusLabels.slice(0, 2),
    fields,
    actions: ['Edit', 'Marketing Permissions', 'Crash Log'],
  }
}

function inferSummaryGrid(pkg: DesignPackage, clover?: CloverAdapterHints): CloverSummaryItem[] {
  if ((clover?.summaryGrid?.length ?? 0) > 0) return clover!.summaryGrid!

  const labels = (pkg.dataModel?.keyFields ?? []).slice(0, 4)
  return labels.map((label, index) => ({
    label,
    value: sampleValueFor(label, index),
    subValue: normalizeKey(label).includes('log') ? 'Activity Log' : undefined,
  }))
}

function inferCards(pkg: DesignPackage, clover?: CloverAdapterHints): CloverCard[] {
  if ((clover?.cards?.length ?? 0) > 0) return clover!.cards!

  const sectionCards = (pkg.sections ?? [])
    .filter(section => !['filter', 'summary', 'data-table', 'logs'].includes(section.role || ''))
    .slice(0, 4)
    .map((section, index) => ({
      title: section.title || `Section ${index + 1}`,
      subtitle: section.description,
      status: pkg.states?.statusLabels?.[index % Math.max(pkg.states?.statusLabels?.length || 1, 1)],
      meta: (section.contents ?? []).slice(0, 4).map((content, contentIndex) => ({
        label: content,
        value: sampleValueFor(content, contentIndex),
      })),
      bullets: (section.contents ?? []).slice(0, 3).map(content => `Support ${content.toLowerCase()} workflow.`),
      actions: ['Usage Time Log', 'Realtime Session', 'Edit'],
    }))

  if (sectionCards.length > 0) return sectionCards

  return [
    {
      title: pkg.meta?.screenName || 'License',
      subtitle: pkg.purpose?.summary,
      status: pkg.states?.statusLabels?.[0] || 'Active',
      meta: (pkg.dataModel?.keyFields ?? []).slice(0, 4).map((field, index) => ({
        label: field,
        value: sampleValueFor(field, index),
      })),
      bullets: (pkg.interactions?.rules ?? []).slice(0, 3),
      actions: ['Edit', 'Usage Log', 'Details'],
    },
  ]
}

function inferTable(pkg: DesignPackage, clover?: CloverAdapterHints): CloverTable {
  if (clover?.table) return clover.table

  const columns = pkg.dataModel?.tableColumns?.length
    ? pkg.dataModel.tableColumns
    : (pkg.dataModel?.keyFields ?? []).slice(0, 6)

  const rows = Array.from({ length: 5 }, (_, rowIndex) =>
    columns.map(column => sampleValueFor(column, rowIndex)),
  )

  return { columns, rows }
}

function badgeClassForValue(value: string, clover?: CloverAdapterHints): string {
  const normalized = normalizeKey(value)
  const mapped = clover?.badgeMapping?.[value]
  if (mapped) return mapped
  if (normalized.includes('integrated') || normalized.includes('verified') || normalized.includes('active') || normalized.includes('success')) return 'badge-green'
  if (normalized.includes('warning') || normalized.includes('pending')) return 'badge-orange'
  if (normalized.includes('error') || normalized.includes('failed') || normalized.includes('cancel')) return 'badge-red'
  if (normalized.includes('off') || normalized.includes('inactive') || normalized.includes('disabled')) return 'badge-gray'
  return 'badge-blue'
}

function renderBadge(value: string, clover?: CloverAdapterHints): string {
  return `<span class="badge ${badgeClassForValue(value, clover)}">${escapeHtml(value)}</span>`
}

function renderSnb(menuGroup: string, menuItems: string[], activeItem: string): string {
  const items = menuItems.map(item =>
    `<a class="snb-item${item === activeItem ? ' active' : ''}">${escapeHtml(item)}</a>`,
  ).join('')

  return `
    <nav class="snb">
      <div class="snb-logo">
        <div class="snb-logo-icon">C</div>
        <span class="snb-logo-name">CLO Admin</span>
      </div>
      <div class="snb-group">
        <div class="snb-group-label">${escapeHtml(menuGroup)}</div>
        ${items}
      </div>
      <div class="snb-group">
        <div class="snb-group-label">Modules</div>
        <a class="snb-item">License</a>
        <a class="snb-item">Software</a>
        <a class="snb-item">Developer</a>
      </div>
    </nav>
  `
}

function renderBreadcrumbs(items: string[]): string {
  return items.map((item, index) => {
    if (index === items.length - 1) {
      return `<span class="breadcrumb-item current">${escapeHtml(item)}</span>`
    }

    return `<a class="breadcrumb-item">${escapeHtml(item)}</a><span class="breadcrumb-sep">/</span>`
  }).join('')
}

function renderListRows(columns: string[], rows: string[][], clover?: CloverAdapterHints): string {
  return rows.map((row, rowIndex) => {
    const cells = columns.map((column, columnIndex) => {
      const value = row[columnIndex] || sampleValueFor(column, rowIndex)
      const normalized = normalizeKey(column)

      if (normalized.includes('integration') || normalized.includes('status') || normalized.includes('api')) {
        return `<td>${renderBadge(value, clover)}</td>`
      }

      if (normalized.includes('email') || normalized.includes('member') || normalized.includes('license id')) {
        return `
          <td class="cell-multi">
            <a class="td-link">${escapeHtml(value)}</a>
            <div class="cell-secondary">${escapeHtml(sampleValueFor('email', rowIndex))}</div>
          </td>
        `
      }

      if (normalized.includes('license')) {
        return `
          <td class="cell-multi">
            <div class="cell-primary">${escapeHtml(value)}</div>
            <div class="cell-secondary">${escapeHtml('Preview value')}</div>
          </td>
        `
      }

      return `<td>${escapeHtml(value)}</td>`
    }).join('')

    return `<tr>${cells}</tr>`
  }).join('')
}

function buildListPreview(pkg: DesignPackage, clover?: CloverAdapterHints): string {
  const screenName = pkg.meta?.screenName || 'Member List'
  const breadcrumbs = inferBreadcrumbs(pkg, clover)
  const menuItems = inferMenuItems(pkg, clover)
  const toolbar = inferToolbar(pkg, clover)
  const table = inferTable(pkg, clover)
  const filterChips = toolbar.filterChips ?? []
  const columns = table.columns ?? []
  const rows = table.rows ?? []

  return `
    ${renderSnb(clover?.menuGroup || 'Account', menuItems, screenName)}
    <div class="topbar">
      <nav class="breadcrumb">${renderBreadcrumbs(breadcrumbs)}</nav>
    </div>
    <div class="page-wrap">
      <div class="content">
        <h1 class="page-title">${escapeHtml(screenName)}</h1>
        <div class="list-layout">
          <div class="list-search-row">
            <input class="search-input" value="${escapeHtml(toolbar.primarySearchPlaceholder || '')}" />
            <input class="search-input" value="${escapeHtml(toolbar.secondarySearchPlaceholder || '')}" />
            <div class="toolbar-actions">
              <button class="btn-primary">Search</button>
              <button class="btn-ghost">Reset</button>
            </div>
          </div>
          ${filterChips.length > 0 ? `
            <div class="list-filter-row">
              ${filterChips.map(chip => `<span class="filter-chip">${escapeHtml(chip)}</span>`).join('')}
            </div>
          ` : ''}
          <div class="result-meta">${escapeHtml(toolbar.resultLabel || `Results ${rows.length}`)}</div>
          <table>
            <thead>
              <tr>${columns.map(column => `<th>${escapeHtml(column)}</th>`).join('')}</tr>
            </thead>
            <tbody>
              ${renderListRows(columns, rows, clover)}
            </tbody>
          </table>
          <div class="pagination">
            <button>&lt;</button>
            <button class="active">1</button>
            <button>2</button>
            <button>&gt;</button>
          </div>
        </div>
      </div>
    </div>
  `
}

function renderDetailFieldRows(fields: CloverField[], clover?: CloverAdapterHints): string {
  return fields.map(field => `
    <div class="field-row">
      <span class="field-label">${escapeHtml(field.label || 'Field')}</span>
      <span class="field-value">
        ${escapeHtml(field.value || 'Sample value')}
        ${field.badge ? renderBadge(field.badge, clover) : ''}
      </span>
    </div>
  `).join('')
}

function renderSummaryGrid(items: CloverSummaryItem[]): string {
  return items.map(item => `
    <div class="info-cell">
      <div class="info-cell-label">${escapeHtml(item.label || 'Metric')}</div>
      <div class="info-cell-value">${escapeHtml(item.value || 'Sample value')}</div>
      ${item.subValue ? `<div class="info-cell-sub">${escapeHtml(item.subValue)}</div>` : ''}
    </div>
  `).join('')
}

function renderCards(cards: CloverCard[], clover?: CloverAdapterHints): string {
  return cards.map((card, index) => `
    <div class="sub-card">
      <div class="sub-card-header">
        <div class="sub-card-title">
          <div class="sub-card-icon">${index + 1}</div>
          ${escapeHtml(card.title || `Card ${index + 1}`)}
        </div>
        ${card.status ? renderBadge(card.status, clover) : ''}
      </div>
      <div class="sub-meta-grid">
        ${(card.meta ?? []).slice(0, 8).map(meta => `
          <div class="sub-meta-cell">
            <div class="sub-meta-label">${escapeHtml(meta.label || 'Meta')}</div>
            <div class="sub-meta-value">
              ${escapeHtml(meta.value || 'Sample')}
              ${meta.badge ? ` ${renderBadge(meta.badge, clover)}` : ''}
            </div>
          </div>
        `).join('')}
      </div>
      ${card.bullets && card.bullets.length > 0 ? `
        <div class="sub-detail">
          <div class="sub-detail-name">${escapeHtml(card.subtitle || 'Description')}</div>
          <div class="sub-detail-desc">
            <ul class="bullet-list">
              ${card.bullets.map(item => `<li>${escapeHtml(item)}</li>`).join('')}
            </ul>
          </div>
        </div>
      ` : ''}
      <div class="sub-actions">
        ${(card.actions ?? []).map((action, actionIndex) => `
          ${actionIndex > 0 ? '<span class="btn-arrow-sep"></span>' : ''}
          <button class="btn-arrow">${escapeHtml(action)}</button>
        `).join('')}
      </div>
    </div>
  `).join('')
}

function buildDetailPreview(pkg: DesignPackage, clover?: CloverAdapterHints): string {
  const screenName = pkg.meta?.screenName || 'Member Detail'
  const breadcrumbs = inferBreadcrumbs(pkg, clover)
  const menuItems = inferMenuItems(pkg, clover)
  const leftPanel = inferLeftPanel(pkg, clover)
  const summaryGrid = inferSummaryGrid(pkg, clover)
  const cards = inferCards(pkg, clover)
  const table = inferTable(pkg, clover)

  return `
    ${renderSnb(clover?.menuGroup || 'Account', menuItems, screenName)}
    <div class="topbar">
      <nav class="breadcrumb">${renderBreadcrumbs(breadcrumbs)}</nav>
    </div>
    <div class="page-wrap">
      <div class="content">
        <h1 class="page-title">${escapeHtml(screenName)}</h1>
        <div class="detail-layout">
          <div>
            <div class="detail-card">
              <div class="detail-card-id">
                <span class="detail-card-id-text">${escapeHtml(leftPanel.title || screenName)}</span>
                <span class="detail-card-id-copy">[]</span>
              </div>
              ${leftPanel.subtitle ? `<div class="detail-card-note">${escapeHtml(leftPanel.subtitle)}</div>` : ''}
              ${leftPanel.badges && leftPanel.badges.length > 0 ? `<div class="detail-card-badges">${leftPanel.badges.map(badge => renderBadge(badge, clover)).join('')}</div>` : ''}
              ${renderDetailFieldRows(leftPanel.fields ?? [], clover)}
              <div class="detail-card-actions">
                ${(leftPanel.actions ?? []).map(action => `<button class="btn-field">${escapeHtml(action)}</button>`).join('')}
              </div>
            </div>
          </div>
          <div class="sections">
            <div class="section-block">
              <div class="section-header">
                <span class="section-title">Summary</span>
                <div class="section-header-actions">
                  ${(pkg.states?.screenStates ?? []).slice(0, 2).map(state => renderBadge(state, clover)).join('')}
                </div>
              </div>
              <div class="info-grid">
                ${renderSummaryGrid(summaryGrid)}
              </div>
            </div>

            <div class="section-block">
              <div class="section-header">
                <span class="section-title">Primary Cards</span>
                <div class="section-header-actions">
                  <button class="btn-edit">Edit</button>
                </div>
              </div>
              ${renderCards(cards, clover)}
            </div>

            ${(table.columns?.length ?? 0) > 0 ? `
              <div class="section-block">
                <div class="section-header">
                  <span class="section-title">Table Detail</span>
                </div>
                <div class="tab-content">
                  <table class="detail-table-preview">
                    <thead>
                      <tr>${(table.columns ?? []).map(column => `<th>${escapeHtml(column)}</th>`).join('')}</tr>
                    </thead>
                    <tbody>
                      ${renderListRows(table.columns ?? [], table.rows ?? [], clover)}
                    </tbody>
                  </table>
                </div>
              </div>
            ` : ''}
          </div>
        </div>
      </div>
    </div>
  `
}

export function buildDesignPreviewHtml(pkg: DesignPackage): string {
  const clover = pkg.adapterHints?.clover
  const layoutPattern = clover?.layoutPattern || pkg.layout?.pattern || pkg.meta?.screenType || 'detail'
  const isList = layoutPattern.includes('list') || pkg.meta?.screenType === 'list'

  const body = isList ? buildListPreview(pkg, clover) : buildDetailPreview(pkg, clover)

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${escapeHtml(pkg.meta?.screenName || 'Design Preview')}</title>
  <style>${cloverPreviewCss}</style>
</head>
<body>
  ${body}
</body>
</html>`
}
