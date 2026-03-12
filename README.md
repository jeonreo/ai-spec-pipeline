# AI Spec Pipeline

비정형 요구사항을 구조화된 산출물로 변환하고, 코드 변경 초안 PR까지 생성하는 로컬 AI 워크플로우 도구입니다.

`Intake → Spec → Jira / QA / Design → Code Analysis → Patch → PR Draft`

스택: .NET 10 백엔드 + React/Vite 프런트엔드 + Claude CLI (또는 Vertex AI Claude / Gemini)

## 파이프라인

```text
Raw input (요구사항, 슬랙 스레드, 미팅 노트)
  → intake.md        (구조화된 요약 + 미결 항목)
  → spec.md          (결정 스펙 — 단일 진실 공급원)
  ├→ jira.json       (에픽 / 스토리)
  ├→ qa.md           (테스트 케이스)
  └→ design.json     (Design Package v1)
  → code-analysis.md (변경 대상 파일 분석 — GitHub FE·BE 동시 검색)
  → patch.json       (실제 파일 변경 코드 — repo / path / content)
  → PR Draft         (FE·BE 저장소에 브랜치 생성 + Draft PR)
```

`spec.md`는 Jira, QA, Design, Code Analysis 단계의 단일 진실 공급원입니다.

## 주요 기능

- **칸반 보드** — 7개 파이프라인 단계를 카드로 표시, 클릭 시 상세 드로어 열림
- **Intake Q&A** — Intake에서 추출된 미결 질문에 답변하고 프로젝트 지식으로 저장
- **Jira 연동** — 드롭다운으로 프로젝트 및 이슈 타입 선택, Jira에 직접 푸시
- **프로젝트 지식** — 여러 세션에 걸쳐 누적되는 확정 결정사항 및 원칙, AI 정리 지원
- **Design Package** — Clover Quick Preview, Figma Make 프롬프트 핸드오프, raw JSON
- **Code Agent** — Spec 기반으로 FE·BE GitHub 저장소를 동시 검색, 변경 대상 파일 분석
- **Patch Agent** — Code Analysis 기반으로 실제 파일 변경 코드 생성 (JSON 배열 포맷)
- **PR Draft 자동 생성** — Patch 결과를 FE·BE 저장소에 브랜치 커밋 후 Draft PR 생성
- **히스토리** — 날짜 필터, 페이지네이션, 세션 복원, 단건 삭제
- **단계별 모델 선택** — UI에서 각 단계별 Claude 모델 설정 가능
- **Vertex AI 지원** — Claude 및 Gemini 모델 선택 가능, `appsettings.json`으로 전환

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

## 사용 흐름

1. 좌측 **Sources**에 요구사항, 슬랙 스레드, 미팅 노트를 붙여넣습니다.
2. **Generate Intake** 클릭 — 구조화된 요약과 미결 질문을 추출합니다.
3. 미결 질문에 답변하고 **확정 후 자동 실행**을 클릭하면 Spec → Jira / QA / Design이 순차·병렬 실행됩니다.
4. 결과를 검토하고 필요 시 직접 수정합니다. 상위 단계가 변경되면 하위 단계는 **stale** 표시됩니다.
5. **Code Agent** 실행 — Spec을 기반으로 FE·BE 저장소에서 관련 파일을 검색하고 변경 대상을 분석합니다.
6. **Patch Agent** 실행 — Code Analysis 기반으로 실제 코드 변경 내용을 JSON으로 생성합니다.
7. **PR 생성** 버튼 클릭 — FE·BE 저장소에 각각 브랜치를 만들고 Draft PR을 생성합니다.
8. Jira에 이슈를 푸시합니다.

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

## 주요 파일

| 파일 | 역할 |
|------|------|
| [backend/LocalCliRunner.Api/Controllers/RunController.cs](backend/LocalCliRunner.Api/Controllers/RunController.cs) | SSE 스트리밍 엔드포인트, 저장소 검색 주입 |
| [backend/LocalCliRunner.Api/Controllers/GitHubController.cs](backend/LocalCliRunner.Api/Controllers/GitHubController.cs) | GitHub 연결 확인, PR 생성 엔드포인트 |
| [backend/LocalCliRunner.Api/Infrastructure/ClaudeVertexRunner.cs](backend/LocalCliRunner.Api/Infrastructure/ClaudeVertexRunner.cs) | Vertex AI Claude REST 러너 (ADC + SSE) |
| [backend/LocalCliRunner.Api/Infrastructure/GeminiVertexRunner.cs](backend/LocalCliRunner.Api/Infrastructure/GeminiVertexRunner.cs) | Vertex AI Gemini gRPC 러너 |
| [backend/LocalCliRunner.Api/Infrastructure/GitHubService.cs](backend/LocalCliRunner.Api/Infrastructure/GitHubService.cs) | GitHub REST API v3 래퍼 |
| [backend/LocalCliRunner.Api/Infrastructure/RepoSearchService.cs](backend/LocalCliRunner.Api/Infrastructure/RepoSearchService.cs) | FE·BE 병렬 코드 검색 서비스 |
| [backend/LocalCliRunner.Api/Infrastructure/PromptBuilder.cs](backend/LocalCliRunner.Api/Infrastructure/PromptBuilder.cs) | 템플릿 기반 프롬프트 빌더 |
| [web/src/App.tsx](web/src/App.tsx) | 루트 상태 + 레이아웃 |
| [web/src/components/KanbanBoard.tsx](web/src/components/KanbanBoard.tsx) | 파이프라인 칸반 뷰 (7단계) |
| [web/src/components/CardDetailDrawer.tsx](web/src/components/CardDetailDrawer.tsx) | 단계 출력 드로어 |
| [web/src/components/GitHubPanel.tsx](web/src/components/GitHubPanel.tsx) | GitHub 저장소 URL 설정 패널 |

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

## 프롬프트 구조

```text
prompts/
  base.system.md
  policy.md
  skills/
    intake/
    spec/
    jira/
    qa/
    design/
    code-analysis/
    patch/
      SKILL.md
      template.md
```

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
