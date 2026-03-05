#!/bin/bash
echo "$OUTPUT_CONTENT" | python3 -c "
import json, sys
try:
    data = json.load(sys.stdin)
except:
    print('유효하지 않은 JSON')
    sys.exit(1)
missing = []
if not data.get('summary'): missing.append('summary')
if not data.get('description'): missing.append('description')
ac = data.get('acceptance_criteria', [])
if len(ac) < 3: missing.append(f'acceptance_criteria (최소 3개, 현재 {len(ac)}개)')
if missing:
    print('누락/부족:', ', '.join(missing))
    sys.exit(1)
" 2>&1
