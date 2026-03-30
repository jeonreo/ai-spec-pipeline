#!/bin/bash
FAIL=0
MISSING=""

if [ -n "$CONTENT_FILE" ] && [ -f "$CONTENT_FILE" ]; then
  CONTENT=$(cat "$CONTENT_FILE")
else
  CONTENT="$OUTPUT_CONTENT"
fi

echo "$CONTENT" | grep -qF '"version"'    || { MISSING="$MISSING \"version\"";    FAIL=1; }
echo "$CONTENT" | grep -qF '"meta"'       || { MISSING="$MISSING \"meta\"";       FAIL=1; }
echo "$CONTENT" | grep -qF '"layout"'     || { MISSING="$MISSING \"layout\"";     FAIL=1; }
echo "$CONTENT" | grep -qF '"sections"'   || { MISSING="$MISSING \"sections\"";   FAIL=1; }
echo "$CONTENT" | grep -qF '"components"' || { MISSING="$MISSING \"components\""; FAIL=1; }
echo "$CONTENT" | grep -qF '"handoff"'    || { MISSING="$MISSING \"handoff\"";    FAIL=1; }

[ $FAIL -ne 0 ] && echo "누락 필드:$MISSING"
exit $FAIL
