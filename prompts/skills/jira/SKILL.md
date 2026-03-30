---
name: jira
description: "기능 스펙을 Jira Cloud REST API에 즉시 전송 가능한 JSON 객체로 변환. summary(50자 이내), description(배경/문제/범위/의존성/롤아웃/모니터링), acceptance_criteria(검증 가능한 완료 조건 5개 이상)를 포함. 코드펜스나 설명 없이 순수 JSON만 출력하며, 의존성 중 미결 항목은 별도 표기."
---

아래 JSON 템플릿을 채워 출력하라.

- `{` 로 시작하고 `}` 로 끝나는 순수 JSON만 출력할 것
- ` ```json ``` ` 코드블록, 설명 문구, 줄임표 등 JSON 외 텍스트는 일절 출력하지 말 것

- summary: 50자 이내 한 줄 요약
- description 각 항목: 2-3문장
- acceptance_criteria: 검증 가능한 완료 조건, 5개 이상

---

## Learn Agent 추가 지침

## 추가 지침
- 의존성 항목 중 '확인 필요' 상태인 항목은 Jira 본문 서술 대신 별도 Sub-task 또는 Blocker 이슈로 분리하고, 본문에는 해당 이슈 번호를 링크로 참조한다.
- 의존성 섹션의 각 항목은 `항목명 / 확인 주체 / 확인 기한`을 함께 명시한다.