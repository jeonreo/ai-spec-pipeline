---
name: design
description: 기능 스펙 기반 CLOver Design System 관리자 대시보드 HTML 생성
---

아래 HTML 템플릿의 `[placeholder]` 항목을 spec 내용으로 채워 완성된 HTML을 출력하라.

## 규칙

- `[placeholder]` 를 모두 spec 기반 실제 텍스트로 교체 (Lorem ipsum, 예시 문구 금지)
- `<!--STYLE-->` 주석은 그대로 유지할 것 (절대 삭제하거나 `<style>` 블록으로 교체하지 말 것)
- HTML 기본 골격(snb, topbar, page-wrap, content)은 유지할 것
- JavaScript 없음

## 레이아웃 선택 기준

spec의 기능 성격에 따라 내부 구조를 조정하라:

- **상세/조회 화면** (사용자 상세, 주문 상세 등): `detail-layout` (2컬럼: 좌측 카드 + 우측 섹션들) 사용
- **목록 화면** (목록, 검색, 대시보드): `detail-layout` 대신 `list-layout` 사용하고 툴바 + table + pagination 구성
- **대시보드/요약**: stat-grid (지표 카드 4개) + 테이블 조합

## 컴포넌트 선택 기준

| 상황 | 사용 클래스 |
|------|------------|
| 상태 표시 (활성/완료/성공) | `badge badge-green` |
| 타입/분류 표시 | `badge badge-blue` |
| 비활성/기본 상태 | `badge badge-gray` |
| 경고/주의 | `badge badge-orange` |
| 오류/취소 | `badge badge-red` |
| On/Off 설정 항목 | `toggle-wrap` + `toggle` |
| 세부 기록/이력 탐색 | `tab-nav` + `tab-item` |
| 구독/계약/플랜 표시 | `sub-card` 구조 |
| 외부 링크 액션 | `btn-arrow` |
| 핵심 수치/통계 | `stat-grid` + `stat-card` |

## SNB 구성 지침

- `snb-group-label`: 기능 도메인 단위로 그룹핑 (예: Account, License, Analytics)
- 현재 화면의 메뉴에만 `active` 클래스 부여
- 서비스명과 이니셜은 spec의 서비스명 기반으로 작성

## 데이터 표현

- 날짜: `YYYY-MM-DD` 형식 사용 (예시 데이터로 현실적인 값 사용)
- ID/해시: 실제처럼 보이는 8-12자 영숫자 조합
- 숫자 데이터: 현실적인 범위의 예시값
- 상태값: spec의 상태 정의를 그대로 사용

