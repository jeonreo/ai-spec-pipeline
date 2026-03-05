#!/bin/bash
set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"

# ─── Colors ───────────────────────────────────────────────────────────
GREEN='\033[0;32m'; RED='\033[0;31m'; CYAN='\033[0;36m'; NC='\033[0m'

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

check_tool "claude" "https://docs.anthropic.com/en/docs/claude-code"
check_tool "dotnet"  "https://dot.net (.NET 10 SDK)"
check_tool "node"    "https://nodejs.org (LTS)"

# ─── npm install ──────────────────────────────────────────────────────
if [ ! -d "$ROOT/node_modules" ]; then
  echo ""
  echo "[ .. ] Running npm install..."
  cd "$ROOT" && npm install
  echo -e "${GREEN}[ OK ] npm install done${NC}"
fi

# ─── Kill port 5001 if busy ───────────────────────────────────────────
if lsof -ti:5001 &>/dev/null; then
  echo "[ .. ] Port 5001 in use — stopping..."
  lsof -ti:5001 | xargs kill -9 2>/dev/null || true
  sleep 1
fi

# ─── Start servers ────────────────────────────────────────────────────
BACKEND_DIR="$ROOT/backend/LocalCliRunner.Api"
BACKEND_LOG="$ROOT/backend.log"
FRONTEND_LOG="$ROOT/frontend.log"

echo ""

# Try to open two Terminal.app windows (works on macOS without iTerm2)
if [[ "$TERM_PROGRAM" == "iTerm.app" ]] || open -Ra "iTerm" 2>/dev/null; then
  # iTerm2: open two tabs via AppleScript
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
  # Terminal.app: open two windows via AppleScript
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
