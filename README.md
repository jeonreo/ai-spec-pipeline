# AI Spec Pipeline

비정형 요구사항 텍스트를 받아 `Intake -> Spec -> Jira / QA / Design`으로 정리하는 로컬 AI workflow 실험 프로젝트입니다.

현재 `Design` 단계는 단순 HTML 생성기가 아니라, 다음 용도로 재사용 가능한 `Design Package v1 JSON`을 생성합니다.

- Clover 스타일 Quick Preview
- Figma Make용 prompt handoff
- Cursor / Claude Code용 구현 가이드

## Pipeline

```text
Raw input
  -> intake.md
  -> spec.md
  -> jira.json
  -> qa.md
  -> design.json
```

`spec.md`는 Jira, QA, Design의 단일 진실 공급원 역할을 합니다.

## Current Scope

- 로컬 Claude CLI 기반 실행
- .NET 백엔드 + React/Vite 프런트
- 단계별 모델 선택 가능
- 히스토리 저장 / 복원 / 단건 삭제
- Design Package 기반 Clover Quick Preview
- Figma Make용 prompt 복사

## Architecture

```text
Browser UI
  -> Local .NET API
  -> PromptBuilder
  -> Claude CLI
  -> Workspace outputs
```

모든 실행은 로컬에서 이루어지며, 각 실행 결과는 `workspaces/local/` 아래에 저장됩니다.

## Requirements

| Tool | Version |
|------|---------|
| Claude CLI | latest |
| .NET SDK | 10+ |
| Node.js | 18+ |

최초 1회:

```bash
claude login
npm install
```

## Run

### Windows

```powershell
run.win.bat
```

또는:

```powershell
.\run.win.ps1
```

### macOS

```bash
chmod +x run.mac.sh
./run.mac.sh
```

기본 실행 주소:

- Frontend: `http://127.0.0.1:5173`
- Backend: `http://127.0.0.1:5001`

## Usage

1. 좌측 Source 영역에 요구사항, Slack 대화, 메모를 붙여넣습니다.
2. `Generate Intake`로 입력을 정리합니다.
3. 필요한 경우 Intake의 미결 항목을 보완합니다.
4. `Generate Spec`으로 Decision Spec을 생성합니다.
5. 우측에서 `Jira`, `QA`, `Design Package`를 각각 생성하거나 `Generate All`로 병렬 실행합니다.

각 출력은 직접 수정할 수 있고, 상위 입력이 바뀌면 하위 단계는 `stale`로 표시됩니다.

## Design Output

현재 Design 단계의 기본 산출물은 `design.json`입니다.

이 JSON에는 다음 정보가 포함됩니다.

- 화면 목적
- 레이아웃 패턴
- 주요 섹션
- 권장 컴포넌트
- 상태 / 인터랙션 규칙
- Figma Make용 handoff prompt
- Clover Quick Preview용 adapter 힌트

프런트 UI에서는 다음 흐름으로 사용합니다.

- `Open Quick Preview`
  - `design.json`을 Clover 스타일 preview로 렌더링
- `Copy for Figma Make`
  - 디자이너가 Figma Make에 바로 붙여넣는 prompt 복사
- `Advanced`
  - raw JSON 확인 / 복사 / 수동 수정

이 구조는 현재 Clover 기반 검증에 사용하고, 이후 Aurora 디자인 시스템으로 adapter와 prompt를 교체하기 쉽게 만들기 위한 목적입니다.

## History

상단 `히스토리` 버튼으로 이전 실행 결과를 열 수 있습니다.

지원 기능:

- 날짜 필터
- 페이지네이션
- 세션 복원
- 단건 삭제

히스토리 복원 시 저장된 `input`, `intake`, `spec`, `jira`, `qa`, `design` 출력이 함께 돌아옵니다.

## Model Settings

상단 `모델 설정`에서 각 단계별 Claude 모델을 바꿀 수 있습니다.

기본적으로는 `pipeline-settings.json`을 통해 관리하며, 현재 저장소에서는 이 파일이 git ignore 되어 있습니다.

예시 모델 전략:

- `intake`: 빠른 정리 위주
- `spec`: 상대적으로 높은 정확도 필요
- `jira`: JSON 변환 위주
- `qa`: 테스트 케이스 정리
- `design`: Design Package 생성

## Workspace Layout

```text
workspaces/local/{date}-{id}/
  input.txt
  prompt.txt
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

기존 히스토리에는 예전 형식의 `design.html`이 남아 있을 수 있으며, 현재 UI는 이를 legacy preview로도 열 수 있습니다.

## Prompt Layout

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
      assets/
        style.css
```

`design/SKILL.md`는 `Design Package v1 JSON` 생성 규칙을, `design/template.md`는 해당 JSON 스키마 예시를 제공합니다.

## Key Files

- [backend/LocalCliRunner.Api/Controllers/RunController.cs](backend/LocalCliRunner.Api/Controllers/RunController.cs)
- [backend/LocalCliRunner.Api/Infrastructure/PromptBuilder.cs](backend/LocalCliRunner.Api/Infrastructure/PromptBuilder.cs)
- [backend/LocalCliRunner.Api/Application/RunStageHandler.cs](backend/LocalCliRunner.Api/Application/RunStageHandler.cs)
- [web/src/App.tsx](web/src/App.tsx)
- [web/src/components/OutputPanel.tsx](web/src/components/OutputPanel.tsx)
- [web/src/components/DesignPackageView.tsx](web/src/components/DesignPackageView.tsx)
- [web/src/designPackage.ts](web/src/designPackage.ts)

## Troubleshooting

### Vite proxy error / wrong port

반드시 `http://127.0.0.1:5173`으로 접속합니다.

### Claude CLI not found

터미널에서 직접 확인합니다.

```bash
claude --help
```

### Backend changes not reflected

백엔드 프로세스가 이전 바이너리를 잡고 있을 수 있으니 재시작합니다.

## Status

현재는 내부 개인 검증 단계입니다.

방향성은 다음 두 가지를 동시에 검증하는 것입니다.

- `spec / jira / qa` handoff 효율
- `Design Package -> Clover Preview / Figma Make` 흐름의 실효성
