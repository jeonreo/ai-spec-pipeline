# AI Spec Pipeline

요구사항 텍스트를 입력하면 로컬 Claude CLI가 자동으로 스펙 문서와 디자인 초안을 생성한다.

```
요구사항 입력
  → intake.md    (문제 정의)
  → spec.md      (기능 스펙)
  → jira.json    (Jira 티켓)
  → qa.md        (QA 문서)
  → design.html  (UI 디자인 초안 — CLOver Design System 기반)
```

## 전제 조건

| 도구 | 버전 | 설치 |
|------|------|------|
| Claude CLI | 최신 | https://docs.anthropic.com/en/docs/claude-code |
| .NET SDK | 10+ | https://dot.net |
| Node.js | 18+ | https://nodejs.org |

Claude CLI 설치 후 로그인 필요:
```
claude login
```

## 시작하기

```
run.bat
```

더블클릭 또는 터미널에서 실행. 자동으로:
1. claude / dotnet / node 설치 여부 확인
2. npm install (최초 1회)
3. 백엔드 서버 시작 (localhost:5001)
4. 프론트엔드 시작 (localhost:5173)

Windows Terminal이 있으면 좌우 분할 패널 한 창으로 뜬다.

## 사용 방법

1. 브라우저에서 `http://localhost:5173` 열기
2. 좌측 텍스트박스에 요구사항 입력
3. **Intake 실행** → 결과가 intake 탭에 자동 표시
4. 결과 확인 후 필요하면 직접 수정
5. **Spec 실행** → intake 내용 기반으로 spec 생성
6. **Jira 실행** / **QA 실행** → spec 기반으로 생성
7. **Design 실행** → spec 기반으로 HTML 디자인 초안 생성 (미리보기 버튼으로 확인)

각 단계는 이전 단계의 출력을 입력으로 사용한다. 탭에서 수정한 내용도 다음 단계에 반영된다.
각 탭에는 실행 소요시간이 표시된다.

## 프로젝트 구조

```
run.bat                         진입점 (전제 조건 체크 + 서버 시작)
run.ps1                         실제 시작 로직 (PowerShell)

prompts/
  base.system.md                공통 지시문
  intake.prompt.md              문제 정의 프롬프트
  spec.prompt.md                기능 스펙 프롬프트
  jira.prompt.md                Jira 티켓 프롬프트
  qa.prompt.md                  QA 문서 프롬프트
  design.prompt.md              UI 디자인 초안 프롬프트 (CLOver Design System)

backend/LocalCliRunner.Api/
  Controllers/RunController.cs  POST /api/run/{profile}, GET /api/run/{jobId}
  Application/                  RunStageHandler (비동기 잡 실행)
  Infrastructure/
    ClaudeCliRunner.cs          claude CLI 프로세스 실행
    PromptBuilder.cs            프롬프트 조합
    PiiTokenizer.cs             개인정보 토큰화/복원
    JobRegistry.cs              인메모리 잡 상태 관리
  Workspace/                    작업 디렉토리 생성 및 파일 저장

web/
  src/
    App.tsx                     메인 앱 (스테이지 실행 + 폴링 + 소요시간 측정)
    api.ts                      runStage / pollUntilDone
    components/
      InputPanel.tsx            요구사항 입력 + 실행 버튼
      OutputTabs.tsx            결과 표시 탭 (소요시간 표시, design 미리보기)

workspaces/local/               실행 결과 저장 (gitignore)
  {timestamp}-{jobId}/
    input.txt                   원본 입력
    prompt.txt                  Claude에 전달한 프롬프트
    out/{stage}.md(json/html)   생성된 결과물
    logs/run.log                stderr 로그
    meta.json                   실행 메타 정보
```

## API

| 메서드 | 경로 | 설명 |
|--------|------|------|
| POST | `/api/run/{profile}` | 스테이지 실행 (intake/spec/jira/qa/design) |
| GET | `/api/run/{jobId}` | 잡 상태 및 결과 조회 |

## 설정

`backend/LocalCliRunner.Api/appsettings.json`

```json
{
  "Cli": {
    "Command": "claude",
    "Args": "-p --model claude-haiku-4-5-20251001 -",
    "TimeoutSeconds": 300
  },
  "Workspace": { "BaseDir": "../../workspaces" },
  "Prompts": { "Dir": "../../prompts" }
}
```

## 개인정보 처리

Claude CLI 전송 전 자동으로 PII를 토큰으로 치환하고, 응답 후 원래 값으로 복원한다.

| 타입 | 감지 방식 |
|------|-----------|
| 이메일 | 정규식 |
| 전화번호 | 한국/국제 형식 정규식 |
| 이름 | `이름:`, `담당자:` 등 레이블 뒤 |
| 주소 | `주소:`, `거주지:` 등 레이블 뒤 |
