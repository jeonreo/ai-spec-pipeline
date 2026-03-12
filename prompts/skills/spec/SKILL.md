---
name: spec
description: 문제 정의를 기반으로 실행 가능한 기능 스펙 작성
---

문제 정의를 분석하여 template.md 구조에 맞는 기능 스펙을 작성한다.

## 대상 코드베이스 아키텍처

### Backend (clo3d-api) — Clean Architecture + CQRS
- **레이어**: Domain → Application → Infrastructure → API
- **Feature 구조**: `api/clo3d-admin/Application.Shared/Features/{Domain}/Commands|Queries/{UseCase}/`
  - `{Verb}{Entity}Request.cs` — IRequest<T> 구현 record
  - `{Verb}{Entity}RequestHandler.cs` — IRequestHandler<TRequest, TResponse>
  - `{Verb}{Entity}Response.cs` — 응답 DTO (필요 시)
- **Domain 구조**: `Domain/Common/src/AggregatesModel/{Domain}Aggregate/`
  - 엔티티: `Tbl{Entity}.cs` (DB 테이블 매핑)
  - 인터페이스: `I{Entity}Repository.cs`
  - 스펙: `Specification/{Entity}Specification.cs`
  - 이벤트: `Events/{Entity}{Event}Event.cs`
- **DB**: EF Core, CLO3DContext / CLOVFContext (멀티 컨텍스트)
- **API 라우팅**: `[Route("api/{domain}")]`, `[Authorize]`, `[ServiceFilter(ClientIPAddressFilterAttribute)]`
- **오류 응답**: `{ "code": "ERROR_CODE", "message": "설명" }`
- **페이징**: offset/limit 방식
- **외부 연동**: AWS S3, CloudFront, Google BigQuery, CloSet, CloVise

### Frontend (clo3d-admin-www) — FSD + Vue3 + CLOver
- **아키텍처**: FSD (Feature-Sliced Design)
  - `entities/{domain}/api/` — `requestV1.get|post|put|delete` 호출
  - `entities/{domain}/model/types.ts` — TypeScript interface/type 정의
  - `entities/{domain}/model/{domain}Queries.ts` — `createCloverQueryKeys` + `usePageContentsQuery`
  - `entities/{domain}/ui/` — Vue SFC 컴포넌트
  - `entities/{domain}/index.ts` — public export
- **컴포넌트**: Vue3 Script Setup + Composition API, `withDefaults(defineProps<Props>())`
- **디자인 시스템**: CLOver Design System 토큰 필수 (임의 px/hex 금지)
  - 색상: `bg-surface-0~11`, `text-main|sub|light|disabled`, `bg-primary|secondary|success|warning`
  - 타이포: `title-1~5`, `body-1~5`, `button-1~5`, `callout-1~5`
  - 스페이싱: `p-s4`, `m-s2`, `gap-s3` (s1px~s20)
  - 반경: `rounded-r1(4px)|r2(8px)|r3(12px)|full`
  - 섀도: `shadow-0|1|2|3`
- **상태관리**: Pinia
- **폼검증**: Vee-Validate + yup
- **주의**: AWS WAF 2,048 바이트 제한 — country 배열 등 큰 파라미터는 압축 처리
- **주석**: 중요 로직은 한국어 주석 필수

## 작성 규칙

- template.md의 섹션 구조를 그대로 유지한다
- 모호한 표현을 사용하지 않는다
- **BE API는 실제 CQRS 구조에 맞게 Command/Query 구분하여 명시**한다
- **FE는 FSD 레이어(entities/features/widgets/pages)와 CLOver 토큰을 명시**한다
- 페이징은 offset/limit, API 버전은 v1 경로 포함
- 예외 처리와 로그 항목은 실제 운영을 고려하여 작성한다
- 불필요한 설명은 제거하고 핵심 정보만 작성한다
