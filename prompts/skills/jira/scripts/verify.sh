#!/bin/bash
FAIL=0
MISSING=""

# CONTENT_FILE 경로를 우선 사용, 없으면 OUTPUT_CONTENT 환경변수 fallback
if [ -n "$CONTENT_FILE" ] && [ -f "$CONTENT_FILE" ]; then
  CONTENT=$(cat "$CONTENT_FILE")
else
  CONTENT="$OUTPUT_CONTENT"
fi

echo "$CONTENT" | grep -qF '"summary"'             || { MISSING="$MISSING \"summary\"";             FAIL=1; }
echo "$CONTENT" | grep -qF '"description"'         || { MISSING="$MISSING \"description\"";         FAIL=1; }
echo "$CONTENT" | grep -qF '"acceptance_criteria"' || { MISSING="$MISSING \"acceptance_criteria\""; FAIL=1; }

[ $FAIL -ne 0 ] && echo "누락 필드:$MISSING"
exit $FAIL
