#!/bin/bash
FAIL=0
MISSING=""

for heading in "## Happy Path" "## Edge Cases" "## Regression Tests"; do
  echo "$OUTPUT_CONTENT" | grep -qF "$heading" || { MISSING="$MISSING $heading"; FAIL=1; }
done

[ $FAIL -ne 0 ] && echo "누락 섹션:$MISSING"
exit $FAIL
