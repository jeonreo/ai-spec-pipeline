#!/bin/bash
OUTPUT="$1"
FAIL=0
MISSING=""

for heading in "## 기능 요약" "## 사용자 흐름" "## UI 구성" "## API 요구사항" "## 예외 처리" "## 로그/모니터링"; do
  grep -qF "$heading" "$OUTPUT" || { MISSING="$MISSING $heading"; FAIL=1; }
done

[ $FAIL -ne 0 ] && echo "누락 섹션:$MISSING"
exit $FAIL
