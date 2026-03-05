#!/bin/bash
FAIL=0
MISSING=""

[[ "$OUTPUT_CONTENT" == *"<!DOCTYPE"*    ]] || { MISSING="$MISSING [<!DOCTYPE html>]"; FAIL=1; }
[[ "$OUTPUT_CONTENT" == *"</html>"*      ]] || { MISSING="$MISSING [</html>]";         FAIL=1; }
[[ "$OUTPUT_CONTENT" == *'class="snb'*   ]] || { MISSING="$MISSING [snb]";             FAIL=1; }
[[ "$OUTPUT_CONTENT" == *'class="card'*  ]] || { MISSING="$MISSING [card]";            FAIL=1; }
[[ "$OUTPUT_CONTENT" == *"<table"*       ]] || { MISSING="$MISSING [<table]";          FAIL=1; }

[ $FAIL -ne 0 ] && echo "누락 요소:$MISSING"
exit $FAIL
