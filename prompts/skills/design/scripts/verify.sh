#!/bin/bash
FAIL=0
MISSING=""

# bash 문자열 비교로 grep 파이프/env var 크기 문제 우회
[[ "$OUTPUT_CONTENT" == *"<!DOCTYPE"*   ]] || { MISSING="$MISSING [<!DOCTYPE html>]"; FAIL=1; }
[[ "$OUTPUT_CONTENT" == *"</html>"*     ]] || { MISSING="$MISSING [</html>]";         FAIL=1; }
[[ "$OUTPUT_CONTENT" == *"class=\"snb"* ]] || { MISSING="$MISSING [snb]";             FAIL=1; }
[[ "$OUTPUT_CONTENT" == *"class=\"card"*]] || { MISSING="$MISSING [card]";            FAIL=1; }
[[ "$OUTPUT_CONTENT" == *"<table"*      ]] || { MISSING="$MISSING [<table]";          FAIL=1; }

[ $FAIL -ne 0 ] && echo "누락 요소:$MISSING"
exit $FAIL
