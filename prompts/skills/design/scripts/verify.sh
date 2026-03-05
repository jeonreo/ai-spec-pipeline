#!/bin/bash
FAIL=0
MISSING=""

for pattern in "<!DOCTYPE html>" "</html>" "<style>" "snb" "card" "<table"; do
  echo "$OUTPUT_CONTENT" | grep -qF "$pattern" || { MISSING="$MISSING [$pattern]"; FAIL=1; }
done

REMAINING=$(echo "$OUTPUT_CONTENT" | grep -c "\[.*\]" 2>/dev/null || echo 0)
if [ "$REMAINING" -gt 2 ]; then
  echo "미치환 placeholder ${REMAINING}개$MISSING"
  exit 1
fi

[ $FAIL -ne 0 ] && echo "누락 요소:$MISSING"
exit $FAIL
