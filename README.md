# AI Spec Pipeline

---

## Problem

기획자가 슬랙 스레드나 미팅 노트를 개발팀에 전달할 때, 항상 같은 문제가 반복됩니다.

- 요구사항이 비정형 텍스트로만 존재 — 개발자마다 다르게 해석
- Jira 티켓, QA 케이스, 디자인 명세, 코드 변경 범위를 각자 따로 작성 — 내용 불일치
- "이거 어디까지 만들어야 해요?" 같은 확인 비용이 계속 발생
- 스펙이 확정된 후에도 FE/BE/QA/Design이 각자 다른 문서를 참고

---

## Solution

비정형 입력을 **단일 스펙 문서(SSOT)** 로 정제하고, 그 스펙에서 모든 산출물을 파생시킵니다.

```
비정형 데이터 → Intake (문제 정의) → Spec (SSOT)
                                          │
                        ┌─────────────────┼──────────────────┐
                        │                 │                  │
                      Jira              UX               FE / BE
                    (티켓)          (Design Pkg)       (코드 초안)
                                                            │
                                                           QA
                                                       (테스트 케이스)
```

스펙이 바뀌면 하위 산출물이 **stale** 표시되어 재생성이 필요한 것을 즉시 알 수 있습니다.

---

## Workflow

### Step 1 — Intake: 비정형 → 구조화 (`Haiku`)

슬랙 스레드, 미팅 노트, 구두 메모 등을 붙여넣으면 문제 정의 문서로 정제합니다.

- 배경 / 목표 / 범위 / 리스크를 섹션별로 정리
- 입력에 없는 내용은 절대 추가하지 않음, 불확실한 항목은 `확인 필요` 표시
- **Q./A. 형식**으로 미결 항목 자동 추출 → 답변 후 다음 단계로

---

### Step 2 — Spec: 단일 진실 공급원 (`Sonnet`)

Intake + 결정사항(Q&A 답변)을 받아 하위 모든 단계가 참조하는 기능 스펙을 생성합니다.

```
Spec (SSOT)
 ├── Jira   — 에픽/스토리 티켓 + 수락 기준
 ├── UX     — Design Package (CLOver 컴포넌트 매핑, Figma Make 핸드오프)
 ├── FE     — Vue3/FSD 레이어 구조, CLOver 토큰, entities/features/pages 경로
 ├── BE     — .NET CQRS Command/Query/Handler, Clean Architecture 레이어
 └── QA     — 정상/엣지/오류 케이스 × 3, 회귀 영향도
```

**Spec이 바뀌면 Jira, UX, FE, BE, QA 모두 stale — 재생성 필요 알림 자동 표시**

Spec에는 비즈니스 정책(`policy.md`)이 자동 적용됩니다.
- 보안: PII 비저장, OAuth2, IP 필터
- 권한: 역할 기반 접근 제어, clo3d-admin 분리
- 규격: API v1 경로, 오류 응답 포맷, EF Core 컨벤션, CLOver 디자인 토큰

---

### Step 3 — Jira: 티켓 자동 생성 (`Haiku`)

Spec에서 에픽/스토리 단위로 분해하여 Jira에 직접 푸시합니다.

- 50자 이내 summary, 2~3문장 description, 검증 가능한 수락 기준 5개 이상
- UI에서 프로젝트 키·이슈 타입 선택 후 원클릭 푸시
- 이후 생성되는 GitHub 브랜치/PR URL이 이 티켓에 자동 Remote Link로 연결

---

### Step 4 — UX: Design Package 생성 (`Haiku`)

Spec의 기능 요약 + UI 구성 섹션에서 Design Package v1 JSON을 생성합니다.

- 레이아웃 패턴: `list-with-toolbar`, `detail-with-sections`, `form-flow`, `dashboard-summary`
- CLOver 컴포넌트 매핑: `PageHeader`, `SearchInput`, `Card`, `Badge`, `Table`, `Modal`, `Toast`
- Clover Quick Preview, Figma Make 프롬프트 핸드오프, raw JSON 탭 제공

---

### Step 5 — FE / BE: 코드 초안 생성 (`Sonnet × 2`)

**Code Analysis** → 실제 GitHub 저장소를 검색해 변경 대상 파일·함수·클래스를 분석합니다.
**Patch** → Code Analysis 기반으로 변경 파일 전체 내용을 JSON으로 생성합니다.

| | FE (clo3d-admin-www) | BE (clo3d-api) |
|---|---|---|
| 아키텍처 | FSD — entities/features/pages | Clean Architecture + CQRS |
| 패턴 | `<script setup>`, `withDefaults(defineProps)` | record Request + Handler 쌍 |
| 규칙 | CLOver 토큰 필수, any 금지, inline 스타일 금지 | Primary constructor, `[Authorize]`, 네임스페이스 규칙 |
| 출력 | `entities/{domain}/api`, `model/types.ts`, Vue SFC | Command/Query/Handler, TblEntity, IRepository |

Patch 출력 포맷: `[{ "repo": "frontend|backend", "path": "...", "content": "...", "comment": "변경 이유" }]`

---

### Step 6 — QA: 테스트 케이스 생성 (`Sonnet`)

Spec에서 섹션별 테스트 케이스를 생성합니다.

- Given/When/Then 또는 입력/기대결과 포맷
- 섹션당 정확히 3케이스 (정상 / 엣지 / 오류)
- 경계값, 권한 오류, 빈 값, 최댓값 엣지 케이스 포함
- 기존 기능의 회귀 영향도 분석

---

### Step 7 — Draft PR 자동 생성

Patch 결과를 FE·BE 저장소에 각각 브랜치 커밋 후 Draft PR을 생성합니다.

1. **브랜치 푸시** — `ai-draft/{date}-{hash}` 브랜치에 patch 파일 커밋
2. **Draft PR 생성** — Spec 제목 기반 PR, 변경 목적/분석 요약 본문 포함
3. **Jira 자동 연결** — 생성된 Jira 티켓에 브랜치/PR URL을 Remote Link로 등록

---

## Example

**입력 (슬랙 스레드 그대로 붙여넣기)**

```
PM: 관리자 페이지에서 사용자를 이메일로 검색하는 기능이 필요해요.
    목록에서 바로 검색되면 좋겠고, 페이지네이션도 유지되어야 합니다.
DEV: 권한 체크는요?
PM: admin 역할만 접근 가능하게요. 일반 사용자는 403이요.
```

**생성 산출물**

| 단계 | 결과 |
|------|------|
| Intake | 배경·목표·범위 정리, Q. "검색 대상 필드가 이메일 외에 있나요?" 추출 |
| Spec | BE: `SearchUsersQuery` + `SearchUsersQueryHandler`, GET `/api/v1/users?email=&offset=&limit=` / FE: `entities/user/api/userApi.ts`, `usePageContentsQuery`, 검색 인풋 컴포넌트 / 권한: `[Authorize(Roles = "Admin")]`, 403 오류 응답 포맷 명시 |
| Jira | Epic: 사용자 이메일 검색 / Story: BE API 구현, FE UI 구현, 권한 처리 — AC 6개 |
| UX | `list-with-toolbar` 레이아웃, `SearchInput` + `Table` + `Pagination` 컴포넌트 매핑 |
| FE | `entities/user/api/userApi.ts`, `model/types.ts`, `UserSearchInput.vue`, `pages/users/ui/UsersPage.vue` 전체 파일 |
| BE | `SearchUsersRequest.cs`, `SearchUsersRequestHandler.cs`, `UserController.cs` 전체 파일 |
| QA | 정상 검색 / 존재하지 않는 이메일 / admin 외 역할 접근 403 케이스 |
| PR | FE·BE 각각 Draft PR 생성 + Jira 티켓에 링크 자동 등록 |

---

## Quick Start

### 1. 토큰 설정

프로젝트 루트에 `.env` 파일 생성:

```
Jira__ApiToken=your_jira_api_token
GitHub__Token=your_github_personal_access_token
```

- Jira 토큰: https://id.atlassian.com/manage-profile/security/api-tokens
- GitHub 토큰: https://github.com/settings/tokens (`repo` 스코프 필요)

> **보안**: 토큰은 반드시 `.env`에만 보관하세요. `appsettings.Development.json`에 토큰을 넣지 마세요 — git 커밋 시 노출됩니다.

### 2. Jira·GitHub 설정

`backend/LocalCliRunner.Api/appsettings.json`:

```json
{
  "Jira": {
    "BaseUrl": "https://your-org.atlassian.net",
    "Email": "you@example.com",
    "DefaultProjectKey": "PROJ",
    "DefaultIssueTypeName": "Epic"
  }
}
```

앱 좌측 **Sources** 패널에서 FE·BE GitHub 저장소 URL 입력 후 저장.

### 3. 설치 및 실행

```bash
npm install
claude login        # Claude CLI 사용 시
```

**Windows**
```powershell
.\run.win.bat
```

**macOS**
```bash
chmod +x run.mac.sh && ./run.mac.sh
```

- 프런트엔드: `http://127.0.0.1:5173`
- 백엔드: `http://127.0.0.1:5001`

### 4. Vertex AI 사용 (선택)

```json
{
  "Vertex": {
    "ProjectId": "your-gcp-project-id",
    "Location": "global",
    "Provider": "claude",
    "DefaultModel": "claude-sonnet-4-6"
  }
}
```

`ProjectId` 비워두면 로컬 Claude CLI 사용 (기본값). Vertex 사용 시 `gcloud auth application-default login` 필요.

---

## 아키텍처

```text
Browser (React/Vite :5173)
  ↓ SSE + REST
.NET API (:5001)
  ↓
PromptBuilder → ClaudeCliRunner / ClaudeVertexRunner / GeminiVertexRunner
  ↓
workspaces/local/{date}-{id}/out/

GitHub REST API v3
  ← GitHubService (code search, file fetch, branch, PR)
  ← RepoSearchService (FE·BE 병렬 검색 → LLM 컨텍스트 주입)
```

### 프롬프트 구조

```text
prompts/
  base.system.md          # 전역 지시 (한글, 없는 내용 추가 금지)
  policy.md               # 비즈니스 정책 (spec 단계에만 주입)
  skills/{stage}/
    SKILL.md              # 에이전트 역할 및 출력 규칙
    template.md           # 출력 구조 참조 템플릿
```

조립 순서: `base.system.md` → `policy.md` (spec만) → `SKILL.md` → `template.md` → 입력

### 워크스페이스 구조

```text
workspaces/local/{date}-{id}/
  input.txt
  out/
    intake.md
    spec.md
    jira.json
    qa.md
    design.json
    code-analysis.md
    patch.json
  logs/
    run.log  meta.json
```

### 주요 파일

| 파일 | 역할 |
|------|------|
| `backend/.../Controllers/RunController.cs` | SSE 스트리밍, 저장소 검색 주입 |
| `backend/.../Controllers/GitHubController.cs` | 브랜치 푸시, PR 생성 |
| `backend/.../Infrastructure/PromptBuilder.cs` | 프롬프트 조립기 |
| `backend/.../Infrastructure/RepoSearchService.cs` | FE·BE 병렬 코드 검색 |
| `backend/.../Infrastructure/GitHubService.cs` | GitHub REST API v3 래퍼 |
| `web/src/App.tsx` | 루트 상태 + 레이아웃 |
| `web/src/components/KanbanBoard.tsx` | 파이프라인 칸반 뷰 (7단계) |

---

## 트러블슈팅

**Claude CLI를 찾을 수 없음**
```bash
claude --help
# 설치: https://docs.anthropic.com/en/docs/claude-code
```

**Jira 토큰 오류** — `.env` 파일에 `Jira__ApiToken` 확인. `run.win.bat` 실행 시 누락이면 자동 안내.

**토큰 revoke 오류** — `appsettings.Development.json`에 실제 토큰을 넣고 git push하면 GitHub이 자동 revoke합니다. 토큰은 `.env`에만 보관하세요.

**GitHub 연결 실패 (✗)** — `.env`에 `GitHub__Token` 확인, `repo` 스코프 필요.

**Vite 프록시 에러** — 프런트엔드는 백엔드 준비까지 최대 30초 폴링. 백엔드 시작 여부 확인.

**Vertex AI ADC 미설정**
```bash
gcloud auth application-default login
```
