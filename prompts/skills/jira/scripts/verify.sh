#!/bin/bash
OUTPUT="$1"

# JSON 유효성
python3 -c "import json,sys; json.load(open('$OUTPUT'))" 2>/dev/null
if [ $? -ne 0 ]; then
  echo "유효하지 않은 JSON"
  exit 1
fi

# 필수 필드 확인
python3 - "$OUTPUT" << 'EOF'
import json, sys
data = json.load(open(sys.argv[1]))
missing = []
if not data.get("summary"): missing.append("summary")
if not data.get("description"): missing.append("description")
ac = data.get("acceptance_criteria", [])
if len(ac) < 3: missing.append(f"acceptance_criteria (최소 3개 필요, 현재 {len(ac)}개)")
if missing:
    print("누락/부족:", ", ".join(missing))
    sys.exit(1)
EOF
