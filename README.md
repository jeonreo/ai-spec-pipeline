# AI Spec Pipeline

비정형 입력(Slack 대화, 메모, 요구사항 텍스트 등)을 **Decision Spec**으로 정리하고
필요한 산출물(Jira 티켓, QA 문서, 디자인 초안)을 생성하는 **AI workflow 실험 프로젝트**입니다.

목표는 IDE와 Slack, AI 서비스 사이에서 반복되는 복사 붙여넣기 작업을 줄이고
맥락 정리, 번역, 이슈 생성 시간을 단축하는 것입니다.

```
요구사항 입력
  → intake.md    (문제 정의)
  → spec.md      (기능 스펙 — 모든 산출물의 Single Source of Truth)
  → jira.json    (Jira 티켓 JSON)
  → qa.md        (QA 테스트 케이스)
  → design.html  (UI 초안 — CLOver Design System 기반)
```

## 대상

PM · 기획자 · 개발자 · 디자이너 — 초기 기능 정의 단계에서 스펙 초안을 빠르게 만들고 싶은 팀.

---

## Architecture

이 프로젝트는 **로컬 Agent Runtime** 구조로 실행됩니다.

```
Browser UI
  → Prompt 생성
  → Local Agent Runtime (Claude CLI)
  → AI Model 실행
  → Structured Outputs 생성
```

Claude CLI는 로컬에서 Agent를 실행하는 Runtime 역할을 합니다.
별도 서버 없이 로컬 환경에서 Claude API를 직접 호출하며,
백엔드(.NET)는 스트리밍 SSE, 히스토리 저장, PII 토큰화 등 파이프라인 조율을 담당합니다.

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
2. 좌측 Sources 영역에 요구사항·Slack 스레드·메모 등 자유 입력
3. **Generate Intake** → 문제 정의 생성
4. Intake의 **결정 필요** 섹션에서 미결 항목 확인 및 답변 작성 (아래 참고)
5. **Generate Spec** → Decision Spec 생성 (이후 모든 산출물의 기준)
6. **⚡ 전체 생성** → Jira · QA · Design 세 가지 동시 생성
7. 각 카드에서 결과 확인 · 직접 수정 가능
8. Design 카드 → **미리보기 열기** 버튼으로 브라우저에서 HTML 확인

> 각 카드의 수정 내용은 다음 단계 실행에 반영된다.
> Spec을 수정하면 하위 Jira · QA · Design 카드에 **stale** 표시가 나타난다.

### 결정 필요 (Q./A. 포맷)

Intake 결과에는 **결정 필요** 섹션이 포함될 수 있다.
AI가 입력에서 판단하기 어렵거나 명시되지 않은 사항을 질문 형태로 추출한다.

```
Q. 비로그인 사용자도 장바구니를 사용할 수 있어야 하나요?
A.

Q. 재고 소진 시 상품을 숨길지, 품절 표시만 할지?
A. 품절 표시로 유지한다.
```

- `A.` 뒤가 비어있으면 → 노란색으로 표시 (미결정)
- `A.` 뒤에 내용이 있으면 → 초록색으로 표시 (결정 완료)
- **편집 버튼**을 눌러 `A.` 뒤에 답변을 직접 작성한 뒤 Generate Spec을 실행하면 결정 사항이 반영된다.

누구나 답변을 작성할 수 있다. 팀 공통 의견이나 이미 알고 있는 제약 조건을 `A.` 뒤에 입력하면 Spec 품질이 높아진다.

### 히스토리

우측 상단 **히스토리** 버튼으로 과거 실행 기록 열람 및 복원 가능.
날짜 필터 · 페이지네이션 지원. 세션의 모든 스테이지 출력물이 함께 저장된다.

### 모델 설정

우측 상단 **모델 설정** 버튼으로 각 스테이지에서 사용할 Claude 모델을 개별 지정할 수 있다.

| 모델 | 특징 | 추천 단계 |
|------|------|-----------| 
| Haiku 4.5 | 빠름 · 저렴 | Intake, Jira, Design |
| Sonnet 4.6 | 균형 | Spec, QA |
| Opus 4.6 | 최고 품질 | 복잡한 Spec |

설정은 서버의 `pipeline-settings.json`에 저장되며 재시작 없이 즉시 반영된다.

---

## 단계별 역할

| 단계 | 역할 | 입력 → 출력 |
|------|------|-------------|
| **Intake** | 비정형 입력 정제 · 문제 정의 | 요구사항 텍스트 → 구조화된 문제 정의 |
| **Spec** | 기능 스펙 정의 (허브) | 문제 정의 → 기능 스펙 (SSoT) |
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
  Controllers/SettingsController.cs  stage별 모델 설정
    GET  /api/settings               현재 설정 조회
    PUT  /api/settings               설정 저장
  Application/RunStageHandler.cs    비동기 잡 실행
  Infrastructure/
    ClaudeCliRunner.cs              claude CLI 프로세스 실행 (스트리밍 지원, 호출별 모델 지정)
    SettingsService.cs              pipeline-settings.json 읽기/쓰기 (stage별 모델 관리)
    PromptBuilder.cs                SKILL.md + template.md 조합, CSS 경로 제공
    PiiTokenizer.cs                 개인정보 토큰화 / 복원
    JobRegistry.cs                  인메모리 잡 상태 관리
  Workspace/WorkspaceManager.cs     실행별 디렉토리 생성 · 히스토리 조회

web/src/
  App.tsx                     스테이지 실행 · 병렬 실행 · 상태 관리
  api.ts                      streamStage / fetchHistory / fetchPolicy
  components/
    SourcePanel.tsx            좌측: 입력 영역 + Intake 실행
    SpecPanel.tsx              중앙: Decision Spec 허브 (섹션 뷰 / 편집 토글)
    OutputPanel.tsx            우측: Jira · QA · Design 카드 (접기/펼치기)
    JiraView.tsx               Jira 생성 폼 (프로젝트 · 이슈 타입 선택)
    HistoryPanel.tsx           히스토리 사이드 패널
    SettingsModal.tsx          모델 설정 모달 (stage별 모델 선택 · 저장)

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
    "DefaultModel": "claude-haiku-4-5-20251001",
    "TimeoutSeconds": 300
  },
  "Workspace": { "BaseDir": "../../workspaces" },
  "Prompts": { "Dir": "../../prompts" }
}
```

`DefaultModel`은 stage별 설정이 없을 때의 fallback 값이다.

**stage별 모델은 웹 UI(모델 설정 버튼)로 변경하거나, 아래 파일을 직접 수정한다.**

`backend/LocalCliRunner.Api/pipeline-settings.json` (서버 첫 실행 시 기본값으로 자동 생성)

```json
{
  "stageModels": {
    "intake": "claude-haiku-4-5-20251001",
    "spec":   "claude-sonnet-4-6",
    "jira":   "claude-haiku-4-5-20251001",
    "qa":     "claude-sonnet-4-6",
    "design": "claude-haiku-4-5-20251001"
  }
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
| `scripts/verify.sh` | 출력 검증 — 실패 시 카드에 ⚠ 경고 표시 |

---

## Model Strategy

각 스테이지는 서로 다른 모델을 사용하도록 설정할 수 있다.
웹 UI **모델 설정** 또는 `pipeline-settings.json` 직접 편집 — 두 가지 방법 모두 지원한다.

| Stage | 기본값 | 이유 |
|-------|--------|------|
| Intake | Haiku 4.5 | 구조화 · 정제 위주 — 빠른 응답 우선 |
| Spec | Sonnet 4.6 | 핵심 허브 — 품질이 모든 산출물에 영향 |
| Jira | Haiku 4.5 | JSON 변환 — 정형 작업, 경량 모델로 충분 |
| QA | Sonnet 4.6 | 테스트 케이스 추론 — 정확도 중요 |
| Design | Haiku 4.5 | HTML 생성 — 템플릿 기반, 경량 모델로 충분 |

변경 내용은 `pipeline-settings.json`에 저장되며 서버 재시작 없이 즉시 반영된다.

---

## 개인정보 처리

Claude CLI 전송 전 자동으로 PII를 토큰으로 치환하고, 응답 수신 후 원래 값으로 복원한다.

| 타입 | 감지 방식 |
|------|-----------|
| 이메일 | 정규식 |
| 전화번호 | 한국 / 국제 형식 |
| 이름 | `이름:`, `담당자:` 등 레이블 패턴 |
| 주소 | `주소:`, `거주지:` 등 레이블 패턴 |

또한 `policy.md`를 Spec 단계에 최상단 정책으로 주입하여
회사 정책이나 보안 기준에 어긋나지 않도록 결과를 제어할 수 있다.

---

## Troubleshooting

설치나 실행 중 문제가 발생하면 프로젝트 폴더에서 Claude CLI를 직접 활용할 수 있습니다.

```bash
claude
```

예시 질문:
> "이 프로젝트를 Windows 환경에서 실행하려고 합니다. 필요한 의존성과 실행 방법을 확인해 주세요."

Claude CLI는 프로젝트 구조를 분석하고 실행 환경을 안내할 수 있습니다.

---

## Roadmap

- [x] 단계별 모델 분리 — 웹 UI + pipeline-settings.json
- [ ] Service Account 기반 Jira 인증
- [ ] FE / BE 개발 산출물 생성 (구현 코드 초안)
- [ ] QA Automation 스크립트 생성
- [ ] Design System 연동 강화 (Figma 토큰 기반)
- [ ] Server 기반 Agent 실행 (로컬 CLI 탈피)
- [ ] Skill 품질 관리 구조 (버전 관리 · A/B 평가)

---

## Status

개인 workflow 실험 단계.
업무 중 반복되는 작업을 줄이는 방향으로 계속 개선 중입니다.
