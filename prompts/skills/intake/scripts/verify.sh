#!/bin/bash
FAIL=0
MISSING=""

if [ -n "$CONTENT_FILE" ] && [ -f "$CONTENT_FILE" ]; then
  CONTENT=$(cat "$CONTENT_FILE")
else
  CONTENT="$OUTPUT_CONTENT"
fi

for heading in "## 문제 정의" "## 배경" "## 목표" "## 범위" "## 리스크" "## 결정 필요"; do
  echo "$CONTENT" | grep -qF "$heading" || { MISSING="$MISSING $heading"; FAIL=1; }
done

[ $FAIL -ne 0 ] && echo "누락 섹션:$MISSING"
exit $FAIL
