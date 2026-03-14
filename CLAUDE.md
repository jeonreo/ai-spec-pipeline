# AI Spec Pipeline — Claude Code 가이드

## 아키텍처

전체 시스템 구조와 다이어그램은 [architecture.md](architecture.md)를 참조.

- 전체 시스템 구조 (컴포넌트 관계)
- 파이프라인 스테이지 흐름 (intake → patch)
- 백엔드 레이어 구조 (Controller/Application/Infrastructure)
- SSE 스트리밍 시퀀스
- 프롬프트 조립 구조
- 워크스페이스 디렉토리 레이아웃
- Runner 선택 로직 (Local CLI vs Vertex AI)

## 스택

- **Backend**: .NET 10, Kestrel (:5001), Clean Architecture + CQRS
- **Frontend**: React 18 + TypeScript + Vite (:5173, proxy → :5001)
- **LLM**: Claude CLI (로컬) 또는 Vertex AI (GCP) — `appsettings.json`의 `Vertex:ProjectId`로 전환
- **실행**: `run.win.bat` (Windows) / `run.mac.sh` (macOS)

## 주요 파일

| 파일 | 역할 |
|------|------|
| `backend/.../Controllers/RunController.cs` | 파이프라인 실행 + SSE 스트리밍 |
| `backend/.../Infrastructure/PromptBuilder.cs` | 프롬프트 조립 |
| `backend/.../Infrastructure/ClaudeCliRunner.cs` | 로컬 CLI 실행 |
| `backend/.../Infrastructure/ClaudeVertexRunner.cs` | Vertex AI 호출 |
| `backend/.../Infrastructure/RepoSearchService.cs` | GitHub 병렬 코드 검색 |
| `web/src/App.tsx` | 프런트 상태 관리 |
| `web/src/components/KanbanBoard.tsx` | 파이프라인 UI |
| `web/src/api.ts` | API 클라이언트 (fetch + SSE) |
| `prompts/base.system.md` | 전역 LLM 지시사항 |
| `prompts/policy.md` | 비즈니스 정책 SSOT |
| `prompts/skills/*/SKILL.md` | 스테이지별 에이전트 역할 |

## 개발 규칙

- `.env` 파일에 `Jira__ApiToken`, `GitHub__Token` 보관 — `appsettings.json`에 절대 커밋 금지
- `policy.md`는 SSOT — 비즈니스 결정사항은 `policy-update` 스테이지로만 갱신
- PII(이메일/전화/API 키)는 LLM 전달 전 `PiiTokenizer`가 자동 마스킹
- 스테이지 출력 JSON은 `StripCodeFence` → `RunVerifyScript` (bash) 순으로 검증
- 코드 검색 예산: 파일당 8KB, 세션당 60KB 상한

## 마이그레이션 계획

GCP/Vertex AI 마이그레이션 계획 → `gcp-migration-plan.md` 참조 (Phase 1 우선순위)
