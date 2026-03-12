## Output Template Reference

```markdown
# 코드 변경 분석

## 분석 요약
{한 문장으로 무엇을 왜 바꾸는지 설명}

---

## Backend 변경 (clo3d-api)

### Application Layer
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `api/clo3d-admin/Application.Shared/Features/{Domain}/Commands/{UseCase}/{Request}.cs` | 신규/수정 | |
| `api/clo3d-admin/Application.Shared/Features/{Domain}/Commands/{UseCase}/{Handler}.cs` | 신규/수정 | |

### Domain Layer
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `Domain/Common/src/AggregatesModel/{Domain}Aggregate/Tbl{Entity}.cs` | 신규/수정 | |
| `Domain/Common/src/AggregatesModel/{Domain}Aggregate/I{Entity}Repository.cs` | 신규/수정 | |

### Infrastructure Layer
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `Infrastructure/Common/src/EntityConfigurations/ModelConfigurations/{Entity}Configuration.cs` | 신규/수정 | |

### API Layer
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `api/clo3d-admin/clo3d-admin-api/Controllers/{Domain}Controller.cs` | 신규/수정 | |

---

## Frontend 변경 (clo3d-admin-www)

### entities 레이어
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `apps/clo3d-admin/src/entities/{domain}/model/types.ts` | 신규/수정 | |
| `apps/clo3d-admin/src/entities/{domain}/api/{domain}Api.ts` | 신규/수정 | |
| `apps/clo3d-admin/src/entities/{domain}/model/{domain}Queries.ts` | 신규/수정 | |
| `apps/clo3d-admin/src/entities/{domain}/ui/{Component}.vue` | 신규/수정 | |

### features / pages 레이어
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `apps/clo3d-admin/src/pages/{page}/ui/{Page}.vue` | 신규/수정 | |

### CLOver 디자인 시스템 체크
- 사용 토큰: {색상/타이포/스페이싱 토큰 목록}
- 주의사항: {AWS WAF 파라미터 압축 필요 여부 등}

---

## 영향 범위
- {변경으로 인해 영향받는 다른 파일이나 기능}

## 리스크 및 주의사항
- {DB 마이그레이션 필요 여부}
- {테스트 필요 항목, 부작용 가능성}
- {CLOver 토큰 위반 가능성}

## 변경 우선순위
1. {먼저 변경해야 할 파일 — 보통 Domain → Application → Infrastructure → API 순}
2. {그 다음}
3. ...
```
