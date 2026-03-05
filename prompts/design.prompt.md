CLOver Design System 기반으로 관리자 대시보드 HTML 1페이지를 생성하라.

## 색상 / 타이포
```
snb-bg:#24283a  page-bg:#f4f6fb  card-bg:#fff  primary:#5b6dc0
text-main:#202020  text-sub:#7d7d7d  border:#e5e7eb  border-soft:#cbcfe0
shadow:0 2px 10px rgba(0,0,0,.04)
font: Inter(Google Fonts)
section-label: 12px/600/#5b6dc0/uppercase/ls1.2px
```

## 레이아웃
```
SNB: 210px fixed left h-100vh bg #24283a
  .snb-header: p 28px 20px
  .snb-item: h44 px-16 flex gap-10 r8 color #d9d9d9 14px
             hover: bg rgba(255,255,255,.08)
             active: bg rgba(255,255,255,.12) color #fff

Header: h58 bg #fff shadow ml-210 sticky top-0 z10 px-24 flex between
  title: 14px/600/uppercase/ls1px

Content: ml-210 min-h-100vh bg #f4f6fb p-24
```

## 컴포넌트
**Button Primary:** bg #5b6dc0 color #fff px-16 py-8 r6 14px/600 shadow 0 2px 6px rgba(91,109,192,.3)
**Button Outline:** transparent border #5b6dc0 color #5b6dc0 px-14 py-6 r6 13px/500
**Card:** bg #fff border #e5e7eb r10 p-20 shadow
**Badge blue:** bg #eef0fc color #5b6dc0 r4 px-8 py-2 11px/600 uppercase
**Badge gray:** bg #f0f0f2 color #7d7d7d r4 px-8 py-2 11px/500

## SNB 아이콘
SVG 없이 유니코드 문자로 표현 (width 18px inline-block text-center)
```
Dashboard  →  ⊞
Spec       →  ≡
Tasks      →  ☑
Design     →  ◈
Settings   →  ⚙
```
스타일: `font-size:16px; width:18px; display:inline-block; text-align:center; opacity:.7`
active/hover: opacity 1

## 출력 규칙
- 완전한 HTML (<!DOCTYPE html> ~ </html>), `<style>`에 CSS + Google Fonts Inter
- SNB 메뉴 5개 (유니코드 아이콘 + 텍스트, 첫 번째 active)
- Header: 페이지 타이틀 + Primary 버튼 1개
- 카드 1개 (badge + title + 본문 + outline 버튼)
- 테이블 1개 (toolbar 검색바 + thead 3컬럼 + tbody 3행 + pagination)
- Section Label로 섹션 구분
- spec 기반 실제 텍스트 사용 (Lorem ipsum 금지)
- JavaScript 없음
