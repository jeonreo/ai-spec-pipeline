# Frontend Architecture — clo3d-admin-www (FSD + Vue3 + CLOver)

## Layer Structure (Feature-Sliced Design)

```
apps/clo3d-admin/src/
├── entities/{domain}/
│   ├── api/{domain}Api.ts         # requestV1.get|post|put|delete 호출
│   ├── model/types.ts             # TypeScript interface/type 정의
│   ├── model/{domain}Queries.ts   # createCloverQueryKeys + usePageContentsQuery
│   ├── ui/{ComponentName}.vue     # 재사용 컴포넌트
│   └── index.ts                   # public export
├── features/{domain}/lib/use{Feature}.ts   # 비즈니스 로직
├── widgets/{widget}/              # 복합 UI 블록
└── pages/{page}/ui/{PageName}.vue # 페이지 컴포넌트
```

## Component Rules
- `<script setup lang="ts">` 필수
- Props: `withDefaults(defineProps<Props>(), { ... })`
- Emits: `defineEmits<Emits>()` 타입 정의
- 상태관리: Pinia
- 폼검증: Vee-Validate + yup
- 금지: Options API, `any` 타입, inline 스타일

## CLOver Design System Tokens (임의 px/hex/Tailwind 금지)
- 색상: `bg-surface-0~11`, `text-main|sub|light|disabled`, `bg-primary|secondary|success|warning`, `border-base`
- 타이포: `title-1~5`, `body-1~5`, `button-1~5`, `callout-1~5`
- 스페이싱: `p-s4`, `m-s2`, `gap-s3` (s1px~s20)
- 반경: `rounded-r1(4px)|r2(8px)|r3(12px)|full`
- 섀도: `shadow-0|1|2|3`

## API & Query Patterns
- API 호출: `requestV1.get|post|put|delete(...)` from `@share/apis/requestV1`
- 쿼리 키: `createCloverQueryKeys({ ... })`
- 페이지 쿼리훅: `usePageContentsQuery<Dto>({ queryKey, queryFn })`

## Special Rules
- AWS WAF 2,048 바이트 제한: country 등 큰 배열 파라미터는 `compressParamsIfHasCountry(params)` 적용
- 중요 비즈니스 로직: 한국어 주석 필수
