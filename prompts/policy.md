# 비즈니스 정책

아래 정책을 모든 문서 생성 시 최우선으로 반영한다.

## 보안 / 개인정보
- 주민등록번호, 여권번호, 금융정보는 시스템에 저장하지 않는다.
- 개인정보 수집 시 반드시 동의 절차를 명시한다.
- 외부 API 연동 시 인증 방식(OAuth2 / API Key / JWT)을 스펙에 반드시 포함한다.
- BE API는 `[Authorize]` + `[ServiceFilter(typeof(ClientIPAddressFilterAttribute))]` 적용이 기본이다.

## 서비스 정책
- 모든 기능은 권한(Role / AdminPolicy) 기반 접근 제어를 기본으로 한다.
- 관리자 기능(clo3d-admin)과 사용자 기능은 명확히 분리한다.
- 변경 이력(수정자, 수정 일시)을 추적해야 하는 항목은 스펙에 명시한다. (`TblAdminHistory` 참고)
- 다국어/다지역: country 배열 파라미터는 AWS WAF 2,048바이트 제한을 고려한다.

## 개발 규칙

### Backend (clo3d-api)
- **아키텍처**: Clean Architecture + CQRS (MediatR)
- **Command/Query 분리**: 데이터 변경은 Command, 조회는 Query
- **API 버전**: `/api/v1/{domain}` 경로 포함
- **오류 응답**: `{ "code": "ERROR_CODE", "message": "설명" }`
- **페이징**: offset/limit 방식
- **DB**: EF Core, Tbl 접두사 엔티티, CLO3DContext / CLOVFContext
- **외부 서비스**: AWS S3/CloudFront(파일), Google BigQuery(분석), CloSet/CloVise(내부)

### Frontend (clo3d-admin-www)
- **아키텍처**: FSD — entities/features/widgets/pages 레이어 엄수
- **프레임워크**: Vue 3 Script Setup + Composition API (Options API 금지)
- **타입**: TypeScript 필수, any 금지, 모든 Props/Emits에 interface 정의
- **디자인**: CLOver Design System 토큰만 사용 (임의 px/hex/Tailwind 클래스 금지)
- **API**: `requestV1.get|post|put|delete` 사용, TanStack Query 캐싱 적용
- **상태**: Pinia 스토어
- **폼**: Vee-Validate + yup 스키마

## Jira 티켓 규칙
- Story Point 기준: 1(2h 이하) / 2(반나절) / 3(하루) / 5(2~3일) / 8(1주)
- 이슈 타입: Epic > Task > Sub-task
- 레이블: `backend`, `frontend`, `infra`, `design` 중 해당 항목 표시
- 기본 프로젝트: CWD

## QA 정책
- 신규 기능은 정상 케이스 + 예외 케이스 + 경계값 케이스를 모두 포함한다.
- 보안 관련 기능(로그인, 권한)은 반드시 부정 케이스를 추가한다.
- API 테스트: 인증 없는 요청(401), 권한 없는 요청(403), 잘못된 입력(400) 케이스 포함.
