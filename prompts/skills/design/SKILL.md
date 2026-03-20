---
name: design
description: Create a design-system-agnostic Design Package v1 JSON from the spec. The current rendering target is Clover Admin Design System.
---

Your job is not to generate HTML directly. Read the spec and output a single `Design Package v1` JSON object that can be used for:

- Clover quick preview rendering
- Figma Make prompt handoff
- Cursor or Claude Code implementation guidance
- FE code-analysis-fe agent input (fsdMapping 제공)

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
- `fsdMapping`: **반드시 포함**. spec에 FE 대상 도메인이 있으면 FSD 레이어별 예상 파일 경로를 추정한다.
  - `entities`: `[{ "path": "entities/{domain}/model/types.ts", "role": "타입 정의" }, ...]`
  - `features`: `[{ "path": "features/{domain}/lib/{hook}.ts", "role": "..." }, ...]`
  - `pages`: `[{ "path": "pages/{page}/ui/{Page}.vue", "role": "페이지 진입점" }, ...]`
  - FE 변경이 없는 스펙이면 각 배열을 `[]`로 출력한다.

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
- `secondarySearchPlaceholder` *(optional)* — 두 번째 검색 입력이 있을 때만 추가 (e.g. Email Address)
- `filterChips` *(optional)* — 탭형 필터 버튼 목록. 각 항목은 `{ label, icon?, active? }` 형태
- `filterResetLabel` *(optional)* — RESET 버튼 텍스트. 필터 리셋 기능이 있을 때만 포함
- `resultLabel` *(optional)* — 검색 결과 수 표시 레이블 (e.g. `"Results 5"`)
- `rowsPerPage` *(optional)* — 페이지당 행 수 컨트롤이 있으면 기본값 포함 (e.g. `20`)

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
- `options` *(optional)* — 카드/행에 contextual action 드롭다운이 있을 때. `[{ label, variant? }]` 형태. `variant: "danger"`이면 빨간색으로 렌더링

#### adapterHints.clover.table

- `columns` — 각 컬럼은 `{ key, label, sortable?, infoTooltip? }` 형태. `sortable: true`이면 헤더에 정렬 아이콘 렌더링. `infoTooltip`이 있으면 `?` 아이콘 표시
- `rows` — 셀 값이 단일 문자열이면 그대로, 복수 뱃지가 필요하면 `{ type: "badge-stack", items: [...] }` 형태로 표현
- `extInfo` *(optional)* — 툴바와 테이블 사이에 표시할 추가 메타 정보 바. 페이지마다 다를 수 있음. `[{ label, value, badge? }]` 배열

## Additional Guidance

- Base everything on the actual spec.
- Do not invent new flows that are not implied by the spec.
- Prefer reusable admin UI patterns over novel compositions.
- Optimize for handoff quality, not visual flourish.
- When the screen clearly matches a Clover admin list or detail pattern, include Clover adapter hints so the preview can look structurally close to the real product.

---

## Learn Agent 추가 지침

## 추가 지침
- design 출력 시작 전, meta.screenName과 purpose.summary가 입력받은 스펙의 기능 요약과 일치하는지 확인한다.
- screenId는 스펙 파일명 또는 기능 식별자 기준으로 작성하며, 다른 기능의 값을 재사용하지 않는다.
- 스펙과 design의 기능 범위가 다를 경우 출력을 중단하고 '입력 스펙 불일치'를 명시한다.