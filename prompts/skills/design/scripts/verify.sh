#!/bin/bash
OUTPUT="$1"
FAIL=0
MISSING=""

for pattern in "<!DOCTYPE html>" "</html>" "<style>" "class=\"snb" "class=\"card" "<table"; do
  grep -qF "$pattern" "$OUTPUT" || { MISSING="$MISSING [$pattern]"; FAIL=1; }
done

# placeholder 미치환 확인
REMAINING=$(grep -c "\[.*\]" "$OUTPUT" 2>/dev/null || echo 0)
if [ "$REMAINING" -gt 2 ]; then
  echo "미치환 placeholder ${REMAINING}개 남음$MISSING"
  exit 1
fi

[ $FAIL -ne 0 ] && echo "누락 요소:$MISSING"
exit $FAIL
