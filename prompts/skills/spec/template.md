## 기능 요약
[이 스펙에서 구현할 기능을 2-4문장으로 요약]

## 사용자 흐름
1. [단계]
2. [단계]
3. [단계]

## UI 구성 (FE — FSD 기준)
| 레이어 | 파일 경로 | 역할 |
|--------|-----------|------|
| entities | `entities/{domain}/model/types.ts` | 타입 정의 |
| entities | `entities/{domain}/api/{domain}Api.ts` | requestV1 호출 |
| entities | `entities/{domain}/model/{domain}Queries.ts` | TanStack Query 훅 |
| entities | `entities/{domain}/ui/{ComponentName}.vue` | UI 컴포넌트 |
| pages | `pages/{page}/ui/{PageName}.vue` | 페이지 진입점 |

### 컴포넌트 상태 및 동작
| 컴포넌트 | CLOver 토큰 | 상태/이벤트 | 동작 |
|---------|------------|------------|------|
| | | | |

## API 요구사항 (BE — CQRS 기준)
| 구분 | 파일 경로 | 메서드 | 경로 | 파라미터 | 응답 |
|------|----------|-------|------|---------|------|
| Command | `Features/{Domain}/Commands/{UseCase}/{UseCase}Request.cs` | POST/PUT/DELETE | `/api/v1/{domain}` | `{UseCase}Request` | `int` or DTO |
| Query | `Features/{Domain}/Queries/{UseCase}/{UseCase}Request.cs` | GET | `/api/v1/{domain}` | query params | `{UseCase}Response` |

## 도메인 모델 변경
| 파일 | 변경 유형 | 내용 |
|------|---------|------|
| `Domain/Common/src/AggregatesModel/{Domain}Aggregate/Tbl{Entity}.cs` | 수정/신규 | |
| `Domain/Common/src/AggregatesModel/{Domain}Aggregate/I{Entity}Repository.cs` | 수정/신규 | |

## 예외 처리
- [케이스]: [처리 방법] → `{ "code": "ERROR_CODE", "message": "설명" }`

## 로그/모니터링
- [이벤트]: [기록 내용]

## 비고
- BE 대상 Product: [MarvelousDesigner / CLO3D / 기타]
- FE 영향 페이지: [페이지 경로]
- 외부 연동 여부: [AWS S3 / CloudFront / BigQuery / CloSet / CloVise / 없음]
