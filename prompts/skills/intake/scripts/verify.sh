#!/bin/bash
FAIL=0
MISSING=""

for heading in "## 문제 정의" "## 배경" "## 목표" "## 범위" "## 리스크" "## 결정 필요"; do
  echo "$OUTPUT_CONTENT" | grep -qF "$heading" || { MISSING="$MISSING $heading"; FAIL=1; }
done

[ $FAIL -ne 0 ] && echo "누락 섹션:$MISSING"
exit $FAIL
