## Output Template Reference

```json
[
  {
    "repo": "frontend",
    "path": "경로/파일명.tsx",
    "content": "파일의 완전한 전체 내용 (수정된 버전)",
    "comment": "변경 이유를 한 줄로 설명"
  },
  {
    "repo": "backend",
    "path": "경로/다른파일.cs",
    "content": "...",
    "comment": "..."
  }
]
```

### 규칙
- `repo`: 반드시 `"frontend"` 또는 `"backend"` 중 하나
- `content`: 반드시 파일 전체 내용 (부분 코드 금지)
- 변경이 없는 파일은 배열에서 제외
- `comment`는 한글로 작성
- 경로는 저장소 루트 기준 상대 경로
