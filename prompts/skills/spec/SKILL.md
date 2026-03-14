---
name: spec
description: 문제 정의를 기반으로 실행 가능한 기능 스펙 작성
---

문제 정의를 분석하여 template.md 구조에 맞는 기능 스펙을 작성한다.

## 작성 규칙

- template.md의 섹션 구조를 그대로 유지한다
- 모호한 표현을 사용하지 않는다
- **BE API는 실제 CQRS 구조에 맞게 Command/Query 구분하여 명시**한다
- **FE는 FSD 레이어(entities/features/widgets/pages)와 CLOver 토큰을 명시**한다
- 페이징은 offset/limit, API 버전은 v1 경로 포함
- 예외 처리와 로그 항목은 실제 운영을 고려하여 작성한다
- 불필요한 설명은 제거하고 핵심 정보만 작성한다
