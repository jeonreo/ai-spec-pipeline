# AI Spec Pipeline

요구사항 텍스트를 입력하면 스펙 문서, Jira 티켓, QA 문서, UI 디자인 초안을 자동 생성하는 AI Native 개발 워크플로우 도구.

```
요구사항 입력
  → intake.md    (문제 정의)
  → spec.md      (기능 스펙)
  → jira.json    (Jira 티켓 JSON)
  → qa.md        (QA 테스트 케이스)
  → design.html  (UI 초안 — CLOver Design System 기반)
```

## 대상

PM · 기획자 · 개발자 · 디자이너 — 초기 기능 정의 단계에서 스펙 초안을 빠르게 만들고 싶은 팀.

---

## 전제 조건

| 도구 | 버전 |
|------|------|
| [Claude CLI](https://docs.anthropic.com/en/docs/claude-code) | 최신 |
| .NET SDK | 10+ |
| Node.js | 18+ |

```bash
claude login   # 최초 1회
```

---

## 실행

**Windows**
```
run.win.bat
```
더블클릭 또는 터미널에서 실행. Windows Terminal이 있으면 좌우 분할 패널 한 창으로 표시된다.

**macOS**
```bash
chmod +x run.mac.sh   # 최초 1회
./run.mac.sh
```
iTerm2가 있으면 탭으로, 없으면 Terminal.app 두 창으로 실행된다.

두 스크립트 모두 자동으로:
1. claude / dotnet / node 설치 확인
2. npm install (최초 1회)
3. 백엔드 시작 — `http://localhost:5001`
4. 프론트엔드 시작 — `http://localhost:5173`

---

## 사용 방법

1. `http://localhost:5173` 열기
2. 좌측 텍스트박스에 요구사항 자유 입력
3. **Intake 실행** → 문제 정의 생성
4. **Spec 실행** → intake 기반 기능 스펙 생성
5. **Jira / QA / Design 병렬 실행** (⚡ 버튼) → spec 기반 세 가지 산출물 동시 생성
6. 각 탭에서 결과 확인 · 직접 수정 가능
7. Design 탭 → **미리보기** 버튼으로 브라우저에서 HTML 확인

> 각 탭의 수정 내용은 다음 단계 실행에 반영된다.

### 히스토리

우측 상단 **히스토리** 버튼으로 과거 실행 기록 열람 및 복원 가능.
날짜 필터 · 페이지네이션 지원. 세션의 모든 스테이지 출력물이 함께 저장된다.

---

## 단계별 역할

| 단계 | 역할 | 입력 → 출력 |
|------|------|-------------|
| **Intake** | 문제 정의 정리 | 요구사항 → 문제 정의 |
| **Spec** | 기능 스펙 정의 | 문제 정의 → 기능 스펙 |
| **Jira** | 개발 작업 단위 변환 | 기능 스펙 → 티켓 JSON |
| **QA** | 테스트 케이스 생성 | 기능 스펙 → 테스트 시나리오 |
| **Design** | UI 초안 생성 | 기능 스펙(기능 요약 + UI 구성) → HTML |

---

## 프로젝트 구조

```
run.win.bat / run.win.ps1       진입점 (Windows)
run.mac.sh                      진입점 (macOS)

prompts/
  base.system.md                공통 지시문
  policy.md                     비즈니스 정책 (Spec 단계에 주입)
  skills/{stage}/
    SKILL.md                    단계별 지시문
    template.md                 출력 템플릿 (placeholder 구조)
    assets/style.css            (design 전용) 서버 후처리로 주입되는 CSS
    scripts/verify.sh           (선택) 출력 검증 스크립트

backend/LocalCliRunner.Api/
  Controllers/RunController.cs
    POST /api/run/stream/{profile}   SSE 스트리밍 실행
    POST /api/run/{profile}          비동기 잡 실행
    GET  /api/run/{jobId}            잡 상태 조회
    GET  /api/history                히스토리 목록 (페이징 · 날짜 필터)
    GET  /api/history/{id}           히스토리 상세 (전체 세션 복원)
    GET  /api/policy                 비즈니스 정책 조회
  Application/RunStageHandler.cs    비동기 잡 실행
  Infrastructure/
    ClaudeCliRunner.cs              claude CLI 프로세스 실행 (스트리밍 지원)
    PromptBuilder.cs                SKILL.md + template.md 조합, CSS 경로 제공
    PiiTokenizer.cs                 개인정보 토큰화 / 복원
    JobRegistry.cs                  인메모리 잡 상태 관리
  Workspace/WorkspaceManager.cs     실행별 디렉토리 생성 · 히스토리 조회

web/src/
  App.tsx                     스테이지 실행 · 병렬 실행 · 상태 관리
  api.ts                      streamStage / fetchHistory / fetchPolicy
  components/
    InputPanel.tsx             기반 단계(Intake·Spec) / 산출물(Jira·QA·Design) 그룹 버튼
    OutputTabs.tsx             결과 탭 · 소요시간 · 복사 · 미리보기
    HistoryPanel.tsx           히스토리 사이드 패널

workspaces/local/               실행 결과 저장 (gitignore)
  {date}-{id}/
    input.txt                  원본 입력
    prompt.txt                 Claude에 전달한 프롬프트
    out/{stage}.md/json/html   생성 결과물
```

---

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

---

## Skills 구조

각 단계는 `prompts/skills/{stage}/` 하위 파일로 동작을 정의한다.

| 파일 | 역할 |
|------|------|
| `SKILL.md` | 단계 지시문 (Claude에게 전달) |
| `template.md` | 출력 템플릿 — `[placeholder]`를 Claude가 채움 |
| `assets/style.css` | (design 전용) 서버에서 `<!--STYLE-->` 마커와 교체 주입 |
| `scripts/verify.sh` | 출력 검증 — 실패 시 탭에 ⚠ 경고 표시 (현재 jira · design 활성) |

`verify.sh`는 `$OUTPUT_CONTENT` 환경변수로 출력 내용을 받는다. 파일 경로 없이 동작하므로 OS · 경로 의존 없음.

---

## 개인정보 처리

Claude CLI 전송 전 자동으로 PII를 토큰으로 치환하고, 응답 수신 후 원래 값으로 복원한다.

| 타입 | 감지 방식 |
|------|-----------|
| 이메일 | 정규식 |
| 전화번호 | 한국 / 국제 형식 |
| 이름 | `이름:`, `담당자:` 등 레이블 패턴 |
| 주소 | `주소:`, `거주지:` 등 레이블 패턴 |
