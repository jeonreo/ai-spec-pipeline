---
name: design
description: Create a design-system-agnostic Design Package v1 JSON from the spec. The current rendering target is Clover Admin Design System.
---

Your job is not to generate HTML directly. Read the spec and output a single `Design Package v1` JSON object that can be used for:

- Clover quick preview rendering
- Figma Make prompt handoff
- Cursor or Claude Code implementation guidance

## Output Rules

- Output valid JSON only.
- Do not use markdown code fences.
- Do not output HTML, CSS, prose, or explanations.
- If information is unclear, stay conservative and capture structure rather than inventing visual detail.
- Keep the package design-system-agnostic by default.
- Put Clover-specific rendering hints only under `adapterHints.clover`.

## Required Top-Level Fields

- `version`
- `meta`
- `purpose`
- `layout`
- `sections`
- `dataModel`
- `components`
- `states`
- `interactions`
- `styleNotes`
- `handoff`

## Field Rules

### meta

- `screenId`: kebab-case
- `screenName`: concise human-readable name
- `screenType`: one of `detail`, `list`, `dashboard`, `form`
- `sourceSpecVersion`: short source label if available, otherwise `"spec-v1"`
- `designSystemTarget`: always `"clover-admin"`
- `locale`: `"ko-KR"`

### purpose

- Summarize the screen purpose in 1-2 sentences.
- Include `primaryUser`, `businessGoal`, and `successCriteria`.

### layout

- `pattern` must be one of:
  - `detail-with-sections`
  - `list-with-toolbar`
  - `dashboard-summary`
  - `form-flow`
- `structure` must be ordered from top to bottom.
- `priority` should reflect user attention order.
- `density` should be `compact`, `comfortable`, or `spacious`.

### sections

Each section must contain:

- `id`
- `title`
- `role`
- `description`
- `contents`

Allowed `role` examples:

- `summary`
- `filter`
- `actions`
- `form`
- `data-table`
- `status`
- `logs`

### dataModel

- Include the main `entities`.
- Include `keyFields`.
- Include `tableColumns` when a table or log list exists.

### components

- `recommended` must use generic component names such as:
  - `PageHeader`
  - `SearchInput`
  - `Button`
  - `Card`
  - `Badge`
  - `Tabs`
  - `Table`
  - `Modal`
  - `Toast`
- `requiredBehaviors` should capture must-have interaction expectations.
- `forbiddenPatterns` should block arbitrary custom UI patterns.

### states

- Split screen-level and component-level states.
- Consider `loading`, `empty`, `error`, and `ready` whenever relevant.

### interactions

- Capture actual interaction rules such as confirmation, validation, sorting, refresh, and keyboard focus.
- Do not add decorative animation guidance.

### styleNotes

- Focus on operational clarity and design-system adherence.
- Avoid marketing-style decoration.
- Prefer semantic state representation over custom styling.

### handoff

- `figmaMakePrompt`: a concise prompt the designer can paste into Figma Make.
- `implementationNotes`: short implementation constraints for Cursor or Claude Code.

### adapterHints.clover

Use this to improve Clover quick preview fidelity. The top-level package must stay design-system-agnostic, but Clover preview hints can be added here.

Prefer including:

- `layoutPattern`: `list-layout`, `detail-layout`, or `dashboard-layout`
- `menuGroup`
- `menuItems`
- `breadcrumbs`
- `toolbar`
- `leftPanel`
- `summaryGrid`
- `cards`
- `table`
- `badgeMapping`
- `preferredBlocks`

#### adapterHints.clover.toolbar

- `primarySearchPlaceholder`
- `secondarySearchPlaceholder`
- `filterChips`
- `resultLabel`

#### adapterHints.clover.leftPanel

- `title`
- `subtitle`
- `badges`
- `fields`
- `actions`

#### adapterHints.clover.summaryGrid

Each item may include:

- `label`
- `value`
- `subValue`

#### adapterHints.clover.cards

Each card may include:

- `title`
- `subtitle`
- `status`
- `meta`
- `bullets`
- `actions`

#### adapterHints.clover.table

- `columns`
- `rows`

## Additional Guidance

- Base everything on the actual spec.
- Do not invent new flows that are not implied by the spec.
- Prefer reusable admin UI patterns over novel compositions.
- Optimize for handoff quality, not visual flourish.
- When the screen clearly matches a Clover admin list or detail pattern, include Clover adapter hints so the preview can look structurally close to the real product.
