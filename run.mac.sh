#!/bin/bash
set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"
APPSETTINGS="$ROOT/backend/LocalCliRunner.Api/appsettings.json"
ENV_FILE="$ROOT/.env"

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

# ─── .env helper ──────────────────────────────────────────────────────
get_env_value() {
  local key=$1
  [ -f "$ENV_FILE" ] && grep -E "^\s*${key}\s*=" "$ENV_FILE" | tail -1 | sed "s/^\s*${key}\s*=\s*//" | tr -d '\r' || echo ""
}

set_env_value() {
  local key=$1 val=$2
  if [ -f "$ENV_FILE" ] && grep -qE "^\s*${key}\s*=" "$ENV_FILE"; then
    sed -i '' "s|^\s*${key}\s*=.*|${key}=${val}|" "$ENV_FILE"
  else
    echo "${key}=${val}" >> "$ENV_FILE"
  fi
}

# ─── Read appsettings.json (non-secret values only) ───────────────────
VERTEX_PROJECT_ID=""
JIRA_BASE_URL=""
JIRA_EMAIL=""

if [ -f "$APPSETTINGS" ] && command -v python3 &>/dev/null; then
  VERTEX_PROJECT_ID=$(python3 -c "import json; d=json.load(open('$APPSETTINGS')); print(d.get('Vertex',{}).get('ProjectId',''))" 2>/dev/null || echo "")
  JIRA_BASE_URL=$(python3 -c "import json; d=json.load(open('$APPSETTINGS')); print(d.get('Jira',{}).get('BaseUrl',''))" 2>/dev/null || echo "")
  JIRA_EMAIL=$(python3 -c "import json; d=json.load(open('$APPSETTINGS')); print(d.get('Jira',{}).get('Email',''))" 2>/dev/null || echo "")
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
    echo -e "${YELLOW}[WARN] ADC credentials not found. Run the following command:${NC}"
    echo "       gcloud auth application-default login"
    echo "       Then restart this script."
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
    echo -e "${YELLOW}[WARN] Claude auth may not be configured.${NC}"
    echo "       Run 'claude login' and try again."
  fi
fi
echo ""

# ─── Jira API Token 체크 (.env → 환경변수 순) ────────────────────────
JIRA_TOKEN=$(get_env_value "Jira__ApiToken")
[ -z "$JIRA_TOKEN" ] && JIRA_TOKEN="${Jira__ApiToken:-}"

if [ -z "$JIRA_TOKEN" ]; then
  echo -e "${YELLOW}[WARN] Jira API Token is not configured.${NC}"
  echo "       BaseUrl : $JIRA_BASE_URL"
  echo "       Email   : $JIRA_EMAIL"
  echo ""
  echo -e "${CYAN}  Generate token: https://id.atlassian.com/manage-profile/security/api-tokens${NC}"
  echo ""
  read -rp "  Enter API Token (press Enter to skip): " inputToken

  if [ -n "$inputToken" ]; then
    inputToken=$(echo "$inputToken" | tr -d '[:space:]')
    set_env_value "Jira__ApiToken" "$inputToken"
    export Jira__ApiToken="$inputToken"
    echo -e "${GREEN}[ OK ] Jira API Token saved to .env${NC}"
  else
    echo -e "${YELLOW}[ -- ] Jira Token skipped (Jira integration will not work)${NC}"
  fi
else
  export Jira__ApiToken="$JIRA_TOKEN"
  masked="${JIRA_TOKEN:0:8}****"
  printf "${GREEN}[ OK ]${NC} %-8s Token OK (%s)\n" "Jira" "$masked"
fi

# ─── Slack Bot Token 체크 (.env → 환경변수 순) ───────────────────────
SLACK_TOKEN=$(get_env_value "Slack__BotToken")
[ -z "$SLACK_TOKEN" ] && SLACK_TOKEN="${Slack__BotToken:-}"

if [ -z "$SLACK_TOKEN" ]; then
  echo -e "${YELLOW}[WARN] Slack Bot Token is not configured.${NC}"
  echo "       Slack 스레드 읽기 기능이 비활성화됩니다."
  echo ""
  echo -e "${CYAN}  Generate token: https://api.slack.com/apps  (channels:history 스코프 필요)${NC}"
  echo ""
  read -rp "  Enter Slack Bot Token (press Enter to skip): " inputSlackToken

  if [ -n "$inputSlackToken" ]; then
    inputSlackToken=$(echo "$inputSlackToken" | tr -d '[:space:]')
    set_env_value "Slack__BotToken" "$inputSlackToken"
    export Slack__BotToken="$inputSlackToken"
    echo -e "${GREEN}[ OK ] Slack Bot Token saved to .env${NC}"
  else
    echo -e "${YELLOW}[ -- ] Slack Token skipped (Slack 스레드 읽기 비활성)${NC}"
  fi
else
  export Slack__BotToken="$SLACK_TOKEN"
  masked="${SLACK_TOKEN:0:8}****"
  printf "${GREEN}[ OK ]${NC} %-8s Token OK (%s)\n" "Slack" "$masked"
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

# ─── Start servers (env vars inherited by child processes) ────────────
BACKEND_DIR="$ROOT/backend/LocalCliRunner.Api"
echo ""

if [[ "$TERM_PROGRAM" == "iTerm.app" ]] || open -Ra "iTerm" 2>/dev/null; then
  # iTerm2: export Jira__ApiToken so the new tab inherits it
  osascript <<APPL
    tell application "iTerm2"
      tell current window
        set backendTab to (create tab with default profile)
        tell backendTab
          tell current session
            write text "export Jira__ApiToken='$Jira__ApiToken' && export Slack__BotToken='$Slack__BotToken' && cd '$BACKEND_DIR' && dotnet run"
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
      do script "export Jira__ApiToken='$Jira__ApiToken' && export Slack__BotToken='$Slack__BotToken' && cd '$BACKEND_DIR' && dotnet run"
      do script "cd '$ROOT' && npm run dev"
      activate
    end tell
APPL
fi

echo -e "${GREEN}Done!${NC} Backend: http://localhost:5001  Frontend: http://localhost:5173"
echo ""
