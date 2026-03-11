#!/bin/bash
set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"
APPSETTINGS="$ROOT/backend/LocalCliRunner.Api/appsettings.json"

# ─── Colors ───────────────────────────────────────────────────────────
GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'

echo ""
echo -e "${CYAN}====================================${NC}"
echo -e "${CYAN}  AI Spec Pipeline - Startup Check  ${NC}"
echo -e "${CYAN}====================================${NC}"
echo ""

# ─── Tool check ───────────────────────────────────────────────────────
check_tool() {
  local name=$1 url=$2
  if ! command -v "$name" &>/dev/null; then
    echo -e "${RED}[FAIL] $name not found${NC}"
    echo "       Install: $url"
    exit 1
  fi
  local ver
  ver=$("$name" --version 2>/dev/null | head -1) || ver="?"
  printf "${GREEN}[ OK ]${NC} %-8s %s\n" "$name" "$ver"
}

# ─── Read appsettings.json ────────────────────────────────────────────
VERTEX_PROJECT_ID=""
JIRA_TOKEN=""
JIRA_BASE_URL=""
JIRA_EMAIL=""

if [ -f "$APPSETTINGS" ] && command -v python3 &>/dev/null; then
  VERTEX_PROJECT_ID=$(python3 -c "import json,sys; d=json.load(open('$APPSETTINGS')); print(d.get('Vertex',{}).get('ProjectId',''))" 2>/dev/null || echo "")
  JIRA_TOKEN=$(python3 -c "import json,sys; d=json.load(open('$APPSETTINGS')); print(d.get('Jira',{}).get('ApiToken',''))" 2>/dev/null || echo "")
  JIRA_BASE_URL=$(python3 -c "import json,sys; d=json.load(open('$APPSETTINGS')); print(d.get('Jira',{}).get('BaseUrl',''))" 2>/dev/null || echo "")
  JIRA_EMAIL=$(python3 -c "import json,sys; d=json.load(open('$APPSETTINGS')); print(d.get('Jira',{}).get('Email',''))" 2>/dev/null || echo "")
fi

# ─── 공통 도구 체크 ───────────────────────────────────────────────────
check_tool "dotnet" "https://dot.net (.NET 10 SDK)"
check_tool "node"   "https://nodejs.org (LTS)"
echo ""

# ─── Runner별 체크 ────────────────────────────────────────────────────
if [ -n "$VERTEX_PROJECT_ID" ]; then
  echo -e "${CYAN}[ .. ] Runner: Vertex AI (ProjectId: $VERTEX_PROJECT_ID)${NC}"

  if ! command -v gcloud &>/dev/null; then
    echo -e "${RED}[FAIL] gcloud CLI not found${NC}"
    echo "       Install: https://cloud.google.com/sdk/docs/install"
    exit 1
  fi
  gver=$(gcloud --version 2>/dev/null | head -1) || gver="?"
  printf "${GREEN}[ OK ]${NC} %-8s %s\n" "gcloud" "$gver"

  echo "[ .. ] Checking gcloud ADC auth..."
  token=$(gcloud auth application-default print-access-token 2>/dev/null || echo "")
  if [ -z "$token" ]; then
    echo -e "${YELLOW}[WARN] ADC 인증 없음 — 다음 명령어로 로그인 필요:${NC}"
    echo "       gcloud auth application-default login"
    echo "       (실행 후 다시 시작하세요)"
    exit 1
  fi
  echo -e "${GREEN}[ OK ] gcloud ADC auth OK${NC}"

else
  echo -e "${CYAN}[ .. ] Runner: Claude CLI (local)${NC}"
  check_tool "claude" "https://docs.anthropic.com/en/docs/claude-code"

  echo "[ .. ] Checking claude auth..."
  if claude --print "ping" &>/dev/null; then
    echo -e "${GREEN}[ OK ] claude auth OK${NC}"
  else
    echo -e "${YELLOW}[WARN] Claude 인증이 안 되어 있을 수 있습니다.${NC}"
    echo "       'claude login' 으로 로그인 후 다시 시도하세요."
  fi
fi
echo ""

# ─── Jira API Token 체크 ─────────────────────────────────────────────
if [ -z "$JIRA_TOKEN" ]; then
  echo -e "${YELLOW}[WARN] Jira API Token이 설정되지 않았습니다.${NC}"
  echo "       BaseUrl : $JIRA_BASE_URL"
  echo "       Email   : $JIRA_EMAIL"
  echo ""
  echo -e "${CYAN}  Jira API Token 발급: https://id.atlassian.com/manage-profile/security/api-tokens${NC}"
  echo ""
  read -rp "  API Token을 입력하세요 (Enter 키로 건너뛰기): " inputToken

  if [ -n "$inputToken" ]; then
    if command -v python3 &>/dev/null; then
      python3 - <<PYEOF
import json
path = "$APPSETTINGS"
with open(path, 'r', encoding='utf-8') as f:
    cfg = json.load(f)
cfg['Jira']['ApiToken'] = "$inputToken".strip()
with open(path, 'w', encoding='utf-8') as f:
    json.dump(cfg, f, indent=2, ensure_ascii=False)
PYEOF
      echo -e "${GREEN}[ OK ] Jira API Token 저장 완료${NC}"
    else
      echo -e "${YELLOW}[WARN] python3 없음 — 수동으로 appsettings.json에 입력하세요.${NC}"
    fi
  else
    echo -e "${YELLOW}[ -- ] Jira Token 건너뜀 (Jira 연동 기능이 동작하지 않을 수 있음)${NC}"
  fi
else
  masked="${JIRA_TOKEN:0:8}****"
  printf "${GREEN}[ OK ]${NC} %-8s Token OK (%s)\n" "Jira" "$masked"
fi
echo ""

# ─── npm install ──────────────────────────────────────────────────────
if [ ! -d "$ROOT/node_modules" ]; then
  echo "[ .. ] Running npm install..."
  cd "$ROOT" && npm install
  echo -e "${GREEN}[ OK ] npm install done${NC}"
fi

# ─── Kill ports if busy ───────────────────────────────────────────────
for port in 5001 5173; do
  if lsof -ti:$port &>/dev/null; then
    echo "[ .. ] Port $port in use — stopping..."
    lsof -ti:$port | xargs kill -9 2>/dev/null || true
    sleep 1
  fi
done

# ─── Start servers ────────────────────────────────────────────────────
BACKEND_DIR="$ROOT/backend/LocalCliRunner.Api"
echo ""

if [[ "$TERM_PROGRAM" == "iTerm.app" ]] || open -Ra "iTerm" 2>/dev/null; then
  # iTerm2
  osascript <<APPL
    tell application "iTerm2"
      tell current window
        set backendTab to (create tab with default profile)
        tell backendTab
          tell current session
            write text "cd '$BACKEND_DIR' && dotnet run"
          end tell
        end tell
        set frontendTab to (create tab with default profile)
        tell frontendTab
          tell current session
            write text "cd '$ROOT' && npm run dev"
          end tell
        end tell
      end tell
    end tell
APPL
else
  # Terminal.app
  osascript <<APPL
    tell application "Terminal"
      do script "cd '$BACKEND_DIR' && dotnet run"
      do script "cd '$ROOT' && npm run dev"
      activate
    end tell
APPL
fi

echo -e "${GREEN}Done!${NC} Backend: http://localhost:5001  Frontend: http://localhost:5173"
echo ""
