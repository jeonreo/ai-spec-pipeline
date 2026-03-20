## Output Template Reference

```markdown
# BE 코드 변경 분석

## 분석 요약
{한 문장으로 무엇을 왜 바꾸는지 설명}

---

## Application Layer
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `Application.Shared/Features/{Domain}/Commands/{UseCase}/{UseCase}Request.cs` | 신규/수정 | |
| `Application.Shared/Features/{Domain}/Commands/{UseCase}/{UseCase}Handler.cs` | 신규/수정 | |

## Domain Layer
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `Domain/Common/src/AggregatesModel/{Domain}Aggregate/Tbl{Entity}.cs` | 신규/수정 | |
| `Domain/Common/src/AggregatesModel/{Domain}Aggregate/I{Entity}Repository.cs` | 신규/수정 | |

## Infrastructure Layer
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `Infrastructure/Common/src/EntityConfigurations/ModelConfigurations/{Entity}Configuration.cs` | 신규/수정 | |

## API Layer
| 파일 경로 | 변경 유형 | 변경 내용 |
|----------|---------|---------|
| `api/clo3d-admin/clo3d-admin-api/Controllers/{Domain}Controller.cs` | 신규/수정 | |

---

## 영향 범위
- {변경으로 인해 영향받는 다른 BE 파일이나 기능}

## DB 마이그레이션
- {필요 여부 및 내용. 없으면 "해당 없음"}

## 리스크 및 주의사항
- {부작용 가능성, 외부 연동 영향 등}

## 변경 우선순위
1. {먼저 변경해야 할 파일 — 보통 Domain → Application → Infrastructure → API 순}
2. {그 다음}
3. ...
```
