#!/bin/bash
FAIL=0
MISSING=""

[[ "$OUTPUT_CONTENT" == *'"summary"'*             ]] || { MISSING="$MISSING \"summary\"";             FAIL=1; }
[[ "$OUTPUT_CONTENT" == *'"description"'*         ]] || { MISSING="$MISSING \"description\"";         FAIL=1; }
[[ "$OUTPUT_CONTENT" == *'"acceptance_criteria"'* ]] || { MISSING="$MISSING \"acceptance_criteria\""; FAIL=1; }

[ $FAIL -ne 0 ] && echo "누락 필드:$MISSING"
exit $FAIL
