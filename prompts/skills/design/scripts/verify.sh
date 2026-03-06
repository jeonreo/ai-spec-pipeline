#!/bin/bash
FAIL=0
MISSING=""

if [ -n "$CONTENT_FILE" ] && [ -f "$CONTENT_FILE" ]; then
  CONTENT=$(cat "$CONTENT_FILE")
else
  CONTENT="$OUTPUT_CONTENT"
fi

echo "$CONTENT" | grep -qF '<!DOCTYPE'    || { MISSING="$MISSING [<!DOCTYPE html>]"; FAIL=1; }
echo "$CONTENT" | grep -qF '</html>'      || { MISSING="$MISSING [</html>]";         FAIL=1; }
echo "$CONTENT" | grep -qF 'class="snb'  || { MISSING="$MISSING [snb]";             FAIL=1; }
echo "$CONTENT" | grep -qF 'class="card' || { MISSING="$MISSING [card]";            FAIL=1; }
echo "$CONTENT" | grep -qF '<table'       || { MISSING="$MISSING [<table]";          FAIL=1; }

[ $FAIL -ne 0 ] && echo "누락 요소:$MISSING"
exit $FAIL
