#!/bin/bash
FAIL=0
MISSING=""

for key in '"summary"' '"description"' '"acceptance_criteria"'; do
  printf '%s' "$OUTPUT_CONTENT" | grep -qF "$key" || { MISSING="$MISSING $key"; FAIL=1; }
done

[ $FAIL -ne 0 ] && echo "누락 필드:$MISSING"
exit $FAIL
