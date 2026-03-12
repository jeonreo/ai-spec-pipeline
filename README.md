# AI Spec Pipeline

비정형 요구사항(슬랙 스레드, 미팅 노트, 메모)을 구조화된 산출물로 변환하고, 코드 변경 초안 PR까지 자동 생성하는 로컬 AI 워크플로우 도구입니다.

스택: .NET 10 백엔드 + React/Vite 프런트엔드 + Claude CLI (또는 Vertex AI Claude / Gemini)

---

## 파이프라인 플로우

```
비정형 데이터
(슬랙 스레드 / 미팅 노트 / 구두 요구사항)
        │
        ▼
┌───────────────┐
│    INTAKE     │  Haiku  ─  구조화된 문제 정의 문서
│               │            미결 항목 Q&A 형태로 추출
└───────┬───────┘
        │  결정사항(A. 답변) 추가 후
        ▼
┌───────────────┐
│     SPEC      │  Sonnet ─  실행 가능한 기능 스펙 (SSOT)
│   (단일 진실)  │            BE: CQRS Command/Query 구조
│               │            FE: FSD 레이어 + CLOver 토큰
└───┬───┬───┬───┘
    │   │   │
    ▼   ▼   ▼
┌──────┐ ┌──────┐ ┌──────────┐
│ JIRA │ │  QA  │ │  DESIGN  │  (Spec 기반 병렬 실행)
│Haiku │ │Sonnet│ │  Haiku   │
└──────┘ └──────┘ └──────────┘
    │
    ▼
┌─────────────────┐
│  CODE ANALYSIS  │  Sonnet ─  GitHub FE·BE 저장소 동시 검색
│                 │            변경 대상 파일·함수·클래스 분석
│                 │            코드 작성 없이 변경 계획만 도출
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│      PATCH      │  Sonnet ─  변경 파일 전체 내용 JSON 생성
│                 │            repo / path / content / comment
└────────┬────────┘
         │
         ▼
  브랜치 커밋 + Draft PR
  (FE·BE 저장소 각각)
  + Jira 티켓에 브랜치/PR 링크 자동 연결
```

---

## 각 단계 상세

### 1. Intake — 문제 정의 (`claude-haiku`)

**입력:** 슬랙 스레드, 미팅 노트, 구두 요구사항 등 비정형 텍스트
**출력:** `intake.md` — 구조화된 문제 정의 문서

하는 일:
- 배경, 목표, 범위, 리스크, 예외 케이스를 섹션별로 정리
- 입력에 없는 내용은 절대 추가하지 않음, 불확실한 항목은 "확인 필요" 표시
- **결정 필요 섹션**: 명시되지 않은 항목을 Q./A. 포맷으로 추출 → PM이 직접 답변

답변 후 "확정 후 자동 실행"을 누르면 Spec → Jira/QA/Design이 자동으로 이어집니다.

---

### 2. Spec — 기능 스펙 SSOT (`claude-sonnet`)

**입력:** intake.md + 결정사항(Q&A 답변)
**출력:** `spec.md` — Jira/QA/Design/Code Analysis 모든 하위 단계의 단일 진실 공급원

하는 일:
- 기능 요약, 사용자 플로우, UI 구성, API 설계, 도메인 변경, 예외 처리, 로그 항목을 한 문서로 정의
- **BE (clo3d-api)**: Clean Architecture + CQRS 기준으로 Command/Query 구분 명시
  - 신규: `{Verb}{Entity}Request.cs` + `{Verb}{Entity}RequestHandler.cs` 쌍
  - 도메인 변경: `TblEntity`, `IEntityRepository`, `EntitySpecification`
  - API 라우팅: `api/v1/{domain}`, 오류 응답 포맷, 페이징 방식(offset/limit)
- **FE (clo3d-admin-www)**: FSD 레이어 구조 + CLOver Design System 토큰 명시
  - `entities/{domain}/api`, `model/types.ts`, `{domain}Queries.ts`, `ui/` 레이어별 역할
  - CLOver 색상/타이포/스페이싱/반경 토큰 지정 (임의 px/hex 금지)
- 비즈니스 정책(`policy.md`) 자동 적용: 보안, 권한, 멀티테넌시, 오류 응답 규칙 등

---

### 3. Jira — 티켓 생성 (`claude-haiku`)

**입력:** spec.md
**출력:** `jira.json` — Jira 에픽/스토리 JSON

하는 일:
- Spec에서 에픽과 스토리 단위로 티켓 분해
- 50자 이내 summary, 2~3문장 description, 5개 이상의 검증 가능한 AC(수락 기준) 생성
- 스토리 포인트 산정 포함
- UI에서 프로젝트 키/이슈 타입을 선택하고 Jira에 직접 푸시 가능
- 생성된 Jira 티켓 키에 GitHub 브랜치/PR URL이 자동으로 Remote Link로 연결됨

---

### 4. QA — 테스트 케이스 (`claude-sonnet`)

**입력:** spec.md
**출력:** `qa.md` — 테스트 케이스 문서

하는 일:
- Given/When/Then 또는 Input/ExpectedResult 포맷으로 작성
- 섹션당 정확히 3개 케이스 (정상, 엣지, 오류)
- 경계값, 권한 오류, 빈 값, 최댓값 등 엣지 케이스 포함
- 기존 기능의 회귀 영향도 분석

---

### 5. Design — 디자인 패키지 (`claude-haiku`)

**입력:** spec.md의 기능 요약 + UI 구성 섹션
**출력:** `design.json` — Design Package v1 JSON

하는 일:
- 레이아웃 패턴 선택: `detail-with-sections`, `list-with-toolbar`, `dashboard-summary`, `form-flow`
- 페이지 구조, 컴포넌트 트리, 데이터 모델, 인터랙션 정의
- `adapterHints.clover`: CLOver 컴포넌트 매핑 (`PageHeader`, `SearchInput`, `Card`, `Badge`, `Table`, `Modal`, `Toast` 등)
- UI에서 Clover Quick Preview, Figma Make 프롬프트 핸드오프, raw JSON 탭으로 확인 가능

---

### 6. Code Analysis — 변경 계획 수립 (`claude-sonnet`)

**입력:** spec.md + GitHub FE·BE 저장소에서 검색된 관련 코드 파일
**출력:** `code-analysis.md` — 파일별 변경 계획 문서

하는 일:
- **저장소 자동 검색**: Spec에서 키워드를 추출해 clo3d-api(BE)와 clo3d-admin-www(FE)를 병렬로 GitHub 코드 검색
- 실제 파일명, 함수명, 클래스명을 언급하며 변경 계획 작성 (코드 작성 없음)
- BE: 어떤 Command/Query/Handler/Entity 파일을 생성·수정할지 명시
- FE: 어떤 entities/features/pages 레이어 파일을 생성·수정할지 명시
- 파일이 없을 경우 아키텍처 지식 기반으로 경로를 추정하고 "추정:" 접두어 표시

---

### 7. Patch — 코드 생성 (`claude-sonnet`)

**입력:** code-analysis.md + 실제 코드 파일 (검색 결과)
**출력:** `patch.json` — 파일 변경 JSON 배열

하는 일:
- Code Analysis에서 지정한 각 파일의 **전체 내용**을 생성 (diff가 아닌 완성본)
- BE 코딩 규칙 적용: CQRS record/Handler 패턴, Primary constructor (C# 12+), `[Authorize]`, 네임스페이스 규칙
- FE 코딩 규칙 적용: `<script setup lang="ts">`, `withDefaults(defineProps)`, CLOver 토큰, TanStack Query 키
- JSON 포맷: `[{ "repo": "frontend|backend", "path": "...", "content": "...", "comment": "변경 이유" }]`

---

### 8. PR Draft 자동 생성

**입력:** patch.json
**출력:** GitHub FE·BE 저장소에 브랜치 + Draft PR

하는 일:
- **브랜치 푸시**: FE·BE 저장소에 각각 `ai-draft/{date}-{hash}` 브랜치 생성 후 patch 파일 커밋
- **Draft PR 생성**: spec.md 제목 기반 PR 제목, 변경 목적/요약/분석 내용을 PR 본문에 포함
- **Jira 연결**: 생성된 Jira 티켓 키에 브랜치 URL과 PR URL을 Remote Link로 자동 등록

---

## 주요 기능

- **칸반 보드** — 7개 파이프라인 단계를 카드로 표시, 클릭 시 상세 드로어 열림
- **Stale 감지** — 상위 단계 출력이 변경되면 하위 단계 카드에 자동으로 stale 표시
- **Intake Q&A** — Intake에서 추출된 미결 질문에 답변하고 프로젝트 지식으로 저장
- **Jira 연동** — 드롭다운으로 프로젝트/이슈 타입 선택, Jira에 직접 푸시 + 브랜치/PR 자동 링크
- **Design Package** — Clover Quick Preview, Figma Make 프롬프트 핸드오프, raw JSON
- **Code Agent** — GitHub 저장소 병렬 검색으로 실제 코드 컨텍스트를 LLM에 주입
- **PR Draft** — 브랜치 푸시와 PR 생성을 별도 단계로 분리 (FE·BE 각각)
- **히스토리** — 날짜 필터, 페이지네이션, 세션 복원, 단건 삭제
- **단계별 모델 선택** — UI에서 각 단계별 Claude 모델 설정 가능
- **Vertex AI 지원** — Claude 및 Gemini 모델 선택 가능, `appsettings.json`으로 전환

---

## 설정

### 1. .env 파일

프로젝트 루트에 `.env` 파일 생성 (gitignore 처리됨):

```
Jira__ApiToken=your_jira_api_token
GitHub__Token=your_github_personal_access_token
```

- **Jira 토큰 발급**: https://id.atlassian.com/manage-profile/security/api-tokens
- **GitHub 토큰 발급**: https://github.com/settings/tokens → `repo` 스코프 필요
- `run.win.ps1` 최초 실행 시 Jira 토큰을 입력하면 자동으로 저장됩니다.

### 2. appsettings.json 설정

`backend/LocalCliRunner.Api/appsettings.json` 수정:

```json
{
  "Jira": {
    "BaseUrl": "https://your-org.atlassian.net",
    "Email": "you@example.com",
    "DefaultProjectKey": "PROJ",
    "DefaultIssueTypeName": "Epic"
  },
  "Vertex": {
    "ProjectId": "",
    "Location": "global",
    "Provider": "claude",
    "DefaultModel": "claude-sonnet-4-6",
    "MaxTokens": 8192
  }
}
```

### 3. GitHub 저장소 설정

앱 좌측 **Sources** 패널 → **GitHub 저장소** 섹션에서 FE·BE 저장소 URL 입력 후 저장:

```
https://github.com/your-org/frontend-repo
https://github.com/your-org/backend-repo
```

저장하면 연결 상태(✓/✗)가 즉시 확인됩니다. Code Agent와 Patch Agent가 양쪽 저장소를 동시에 검색합니다.

### 4. Vertex AI (선택)

**Claude 모델 사용 (권장)**

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

**Gemini 모델 사용**

```json
{
  "Vertex": {
    "ProjectId": "your-gcp-project-id",
    "Location": "us-central1",
    "Provider": "gemini",
    "DefaultModel": "gemini-2.0-flash-001"
  }
}
```

`ProjectId`를 비워두면 로컬 Claude CLI를 사용합니다 (기본값).
Vertex AI 사용 시 `gcloud auth application-default login` 필요.

### 5. 최초 설치

```bash
npm install
claude login    # Claude CLI 사용 시
```

---

## 실행

### Windows

```powershell
.\run.win.ps1
```

또는 `run.win.bat` 더블클릭.

### macOS

```bash
chmod +x run.mac.sh
./run.mac.sh
```

백엔드와 프런트엔드가 별도 터미널 탭/창에서 실행됩니다.

- 프런트엔드: `http://127.0.0.1:5173`
- 백엔드: `http://127.0.0.1:5001`

---

## 사용 흐름

1. 좌측 **Sources**에 요구사항, 슬랙 스레드, 미팅 노트를 붙여넣습니다.
2. **Generate Intake** 클릭 — 구조화된 요약과 미결 질문(Q.)을 추출합니다.
3. 각 Q.에 A.를 채운 뒤 **확정 후 자동 실행** 클릭 → Spec 생성 후 Jira / QA / Design이 병렬 실행됩니다.
4. 결과를 검토하고 필요 시 직접 수정합니다. 상위 단계가 변경되면 하위 단계는 **stale** 표시됩니다.
5. **Code Agent** 실행 — GitHub에서 관련 파일을 검색하고 변경 대상 파일·함수를 분석합니다.
6. **Patch Agent** 실행 — Code Analysis 기반으로 실제 코드 변경 파일 전체를 JSON으로 생성합니다.
7. **브랜치 푸시** — FE·BE 저장소에 각각 브랜치를 생성하고 커밋합니다.
8. **PR 생성** — Draft PR을 생성합니다. Jira 티켓이 있으면 브랜치/PR URL이 자동으로 연결됩니다.
9. Jira 에 이슈를 푸시합니다 (별도 버튼).

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

모든 실행은 로컬에서 이루어지며, 결과는 `workspaces/local/` 하위에 저장됩니다.

---

## 주요 파일

| 파일 | 역할 |
|------|------|
| [backend/LocalCliRunner.Api/Controllers/RunController.cs](backend/LocalCliRunner.Api/Controllers/RunController.cs) | SSE 스트리밍 엔드포인트, 저장소 검색 주입 |
| [backend/LocalCliRunner.Api/Controllers/GitHubController.cs](backend/LocalCliRunner.Api/Controllers/GitHubController.cs) | GitHub 연결 확인, 브랜치 푸시, PR 생성 |
| [backend/LocalCliRunner.Api/Infrastructure/ClaudeVertexRunner.cs](backend/LocalCliRunner.Api/Infrastructure/ClaudeVertexRunner.cs) | Vertex AI Claude REST 러너 (ADC + SSE) |
| [backend/LocalCliRunner.Api/Infrastructure/GeminiVertexRunner.cs](backend/LocalCliRunner.Api/Infrastructure/GeminiVertexRunner.cs) | Vertex AI Gemini 러너 |
| [backend/LocalCliRunner.Api/Infrastructure/GitHubService.cs](backend/LocalCliRunner.Api/Infrastructure/GitHubService.cs) | GitHub REST API v3 래퍼 |
| [backend/LocalCliRunner.Api/Infrastructure/RepoSearchService.cs](backend/LocalCliRunner.Api/Infrastructure/RepoSearchService.cs) | FE·BE 병렬 코드 검색 서비스 |
| [backend/LocalCliRunner.Api/Infrastructure/PromptBuilder.cs](backend/LocalCliRunner.Api/Infrastructure/PromptBuilder.cs) | 템플릿 기반 프롬프트 조립기 |
| [web/src/App.tsx](web/src/App.tsx) | 루트 상태 + 레이아웃 |
| [web/src/components/KanbanBoard.tsx](web/src/components/KanbanBoard.tsx) | 파이프라인 칸반 뷰 (7단계) |
| [web/src/components/CardDetailDrawer.tsx](web/src/components/CardDetailDrawer.tsx) | 단계 출력 드로어 |
| [web/src/components/GitHubPanel.tsx](web/src/components/GitHubPanel.tsx) | GitHub 저장소 URL 설정 패널 |

---

## 워크스페이스 구조

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
    run.log
    meta.json
```

---

## 프롬프트 구조

```text
prompts/
  base.system.md          # 전역 시스템 지시 (한글 출력, 없는 내용 추가 금지)
  policy.md               # 비즈니스 정책 (spec 단계에만 주입)
  skills/
    intake/
    spec/
    jira/
    qa/
    design/
    code-analysis/
    patch/
      SKILL.md            # 에이전트 역할 및 출력 규칙
      template.md         # 출력 구조 참조 템플릿
```

프롬프트 조립 순서: `base.system.md` → (`policy.md`) → `SKILL.md` → `template.md` → `입력 텍스트`

---

## 트러블슈팅

**시작 시 Vite 프록시 에러**
프런트엔드는 백엔드가 준비될 때까지 최대 30초간 폴링합니다. 에러가 지속되면 백엔드가 정상 시작했는지 확인하세요.

**Claude CLI를 찾을 수 없음**
```bash
claude --help
```
설치: https://docs.anthropic.com/en/docs/claude-code

**Jira 토큰 오류**
프로젝트 루트의 `.env` 파일 확인: `Jira__ApiToken=<token>`
`run.win.ps1`이 토큰 누락 시 자동으로 안내합니다.

**GitHub 연결 실패 (✗)**
`.env` 파일에 `GitHub__Token`이 설정되어 있는지 확인하세요.
토큰에 `repo` 스코프가 있어야 합니다.
`run.win.ps1` 재시작 시 토큰이 백엔드 프로세스에 환경변수로 주입됩니다.

**백엔드 변경사항이 반영되지 않음**
이전 바이너리가 실행 중일 수 있으니 백엔드 프로세스를 재시작하세요.

**Vertex AI: ADC 미설정**
```bash
gcloud auth application-default login
```
