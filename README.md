# AI Spec Pipeline

비정형 요구사항을 구조화된 산출물로 변환하는 로컬 AI 워크플로우 도구입니다.

`Intake → Spec → Jira / QA / Design`

스택: .NET 10 백엔드 + React/Vite 프런트엔드 + Claude CLI (또는 Vertex AI)

## 파이프라인

```text
Raw input (요구사항, 슬랙 스레드, 미팅 노트)
  → intake.md      (구조화된 요약 + 미결 항목)
  → spec.md        (결정 스펙 — 단일 진실 공급원)
  → jira.json      (에픽 / 스토리)
  → qa.md          (테스트 케이스)
  → design.json    (Design Package v1)
```

`spec.md`는 Jira, QA, Design 단계의 단일 진실 공급원입니다.

## 주요 기능

- **칸반 보드** — 파이프라인 단계를 카드로 표시, 클릭 시 상세 드로어 열림
- **Intake Q&A** — Intake에서 추출된 미결 질문에 답변하고 프로젝트 지식으로 저장
- **Jira 연동** — 드롭다운으로 프로젝트 및 이슈 타입 선택, Jira에 직접 푸시
- **프로젝트 지식** — 여러 세션에 걸쳐 누적되는 확정 결정사항 및 원칙, AI 정리 지원
- **Design Package** — Clover Quick Preview, Figma Make 프롬프트 핸드오프, raw JSON
- **히스토리** — 날짜 필터, 페이지네이션, 세션 복원, 단건 삭제
- **단계별 모델 선택** — UI에서 각 단계별 Claude 모델 설정 가능
- **Vertex AI 지원** — `appsettings.json`의 `Vertex.ProjectId` 설정으로 러너 전환

## 설정

### 1. Jira API 토큰

프로젝트 루트에 `.env` 파일 생성 (gitignore 처리됨):

```
Jira__ApiToken=your_token_here
```

토큰 발급: https://id.atlassian.com/manage-profile/security/api-tokens

run 스크립트 최초 실행 시 토큰을 입력하면 자동으로 저장됩니다.

### 2. appsettings.json 설정

`backend/LocalCliRunner.Api/appsettings.json` 수정:

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

### 3. Vertex AI (선택)

Claude CLI 대신 Vertex AI(Gemini)를 사용하려면 `Vertex.ProjectId` 설정:

```json
{
  "Vertex": {
    "ProjectId": "your-gcp-project-id",
    "Location": "us-central1",
    "DefaultModel": "gemini-2.0-flash-001"
  }
}
```

`gcloud auth application-default login` 필요.
`ProjectId`를 비워두면 Claude CLI를 사용합니다 (기본값).

### 4. 최초 설치

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
3. 가운데 **Decisions** 패널에서 미결 질문에 답변합니다.
4. **+ 결정사항 추가**로 Q&A 쌍을 **프로젝트 지식**에 저장합니다.
5. **Generate Spec** 클릭 — 결정 스펙을 생성합니다.
6. **Jira**, **QA**, **Design Package**를 개별 생성하거나 **Generate All**로 병렬 실행합니다.
7. 준비되면 Jira에 푸시합니다.

출력물은 직접 수정 가능합니다. 상위 단계가 변경되면 하위 단계는 **stale** 표시됩니다.

## 아키텍처

```text
Browser (React/Vite :5173)
  ↓ SSE + REST
.NET API (:5001)
  ↓
PromptBuilder → Claude CLI / Vertex AI
  ↓
workspaces/local/{date}-{id}/out/
```

모든 실행은 로컬에서 이루어지며, 결과는 `workspaces/local/` 하위에 저장됩니다.

## 주요 파일

| 파일 | 역할 |
|------|------|
| [backend/LocalCliRunner.Api/Controllers/RunController.cs](backend/LocalCliRunner.Api/Controllers/RunController.cs) | SSE 스트리밍 엔드포인트 |
| [backend/LocalCliRunner.Api/Infrastructure/PromptBuilder.cs](backend/LocalCliRunner.Api/Infrastructure/PromptBuilder.cs) | 템플릿 기반 프롬프트 빌더 |
| [backend/LocalCliRunner.Api/Application/RunStageHandler.cs](backend/LocalCliRunner.Api/Application/RunStageHandler.cs) | 단계별 실행 로직 |
| [backend/LocalCliRunner.Api/Infrastructure/GeminiVertexRunner.cs](backend/LocalCliRunner.Api/Infrastructure/GeminiVertexRunner.cs) | Vertex AI 러너 |
| [web/src/App.tsx](web/src/App.tsx) | 루트 상태 + 레이아웃 |
| [web/src/components/KanbanBoard.tsx](web/src/components/KanbanBoard.tsx) | 파이프라인 칸반 뷰 |
| [web/src/components/CardDetailDrawer.tsx](web/src/components/CardDetailDrawer.tsx) | 단계 출력 드로어 |
| [web/src/components/SourcePanel.tsx](web/src/components/SourcePanel.tsx) | 입력 + Jira 설정 |
| [web/src/components/ProjectKnowledgePanel.tsx](web/src/components/ProjectKnowledgePanel.tsx) | 프로젝트 지식 베이스 |

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
run 스크립트가 토큰 누락 시 자동으로 안내합니다.

**백엔드 변경사항이 반영되지 않음**
이전 바이너리가 실행 중일 수 있으니 백엔드 프로세스를 재시작하세요.

**Vertex AI: ADC 미설정**
```bash
gcloud auth application-default login
```
