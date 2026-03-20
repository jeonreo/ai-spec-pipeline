## Output Template Reference

```markdown
# FE 코드 변경 분석

## 분석 요약
{한 문장으로 무엇을 왜 바꾸는지 설명}

## Design Package 연계
{design.json이 제공된 경우: 적용한 Design Package 정보 요약. 없으면 "Design Package 미제공 — spec 기반 분석"}
- 채택한 fsdMapping: {있으면 명시, 없으면 자체 추정}
- 사용할 CLOver 컴포넌트: {design.components.recommended 또는 추정}

---

## entities 레이어
| 파일 경로 | 변경 유형 | 변경 내용 | 출처 |
|----------|---------|---------|------|
| `apps/clo3d-admin/src/entities/{domain}/model/types.ts` | 신규/수정 | | spec/design |
| `apps/clo3d-admin/src/entities/{domain}/api/{domain}Api.ts` | 신규/수정 | | spec |
| `apps/clo3d-admin/src/entities/{domain}/model/{domain}Queries.ts` | 신규/수정 | | spec |
| `apps/clo3d-admin/src/entities/{domain}/ui/{Component}.vue` | 신규/수정 | | design |

## features 레이어
| 파일 경로 | 변경 유형 | 변경 내용 | 출처 |
|----------|---------|---------|------|
| `apps/clo3d-admin/src/features/{domain}/lib/{hook}.ts` | 신규/수정 | | spec |

## pages 레이어
| 파일 경로 | 변경 유형 | 변경 내용 | 출처 |
|----------|---------|---------|------|
| `apps/clo3d-admin/src/pages/{page}/ui/{Page}.vue` | 신규/수정 | | design |

---

## CLOver 디자인 시스템 체크
- 사용 토큰: {색상/타이포/스페이싱 CLOver 토큰 목록}
- 사용 컴포넌트: {CLOver 컴포넌트 목록}
- 주의사항: {AWS WAF 파라미터 압축 필요 여부 등}

## 영향 범위
- {변경으로 인해 영향받는 다른 FE 파일이나 기능}

## 리스크 및 주의사항
- {CLOver 토큰 위반 가능성}
- {기존 컴포넌트 재사용 가능 여부}
- {API 타입 불일치 등}

## 변경 우선순위
1. {먼저 변경해야 할 파일 — 보통 types → api → queries → ui → page 순}
2. {그 다음}
3. ...
```
