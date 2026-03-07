$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Check-Tool($name, $installUrl) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Write-Host "[FAIL] $name not found" -ForegroundColor Red
        Write-Host "       Install: $installUrl"
        Read-Host "`nPress Enter to exit"
        exit 1
    }
    try { $ver = & $name --version 2>$null | Select-Object -First 1 } catch { $ver = "?" }
    Write-Host "[ OK ] $($name.PadRight(8)): $ver" -ForegroundColor Green
}

function Stop-PortIfBusy([int]$port) {
    $lines = netstat -ano 2>$null | Select-String "TCP\s+\S+:$port\s"
    if (-not $lines) { return }

    $processId = (($lines[0] -split '\s+') | Where-Object { $_ -match '^\d+$' } | Select-Object -Last 1)
    if ($processId) {
        Write-Host "[ .. ] Port $port in use (PID $processId) - stopping..."
        Stop-Process -Id ([int]$processId) -Force -ErrorAction SilentlyContinue
        Start-Sleep 1
    }
}

Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "  AI Spec Pipeline - Startup Check" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

Check-Tool "claude" "https://docs.anthropic.com/en/docs/claude-code"
Check-Tool "dotnet"  "https://dot.net (.NET 10 SDK)"
Check-Tool "node"    "https://nodejs.org (LTS)"

# npm install
if (-not (Test-Path "$root\node_modules")) {
    Write-Host "`n[ .. ] Running npm install..."
    Push-Location $root
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] npm install failed" -ForegroundColor Red
        Read-Host "Press Enter to exit"; exit 1
    }
    Pop-Location
    Write-Host "[ OK ] npm install done" -ForegroundColor Green
}

# Kill app ports if busy
Stop-PortIfBusy 5001
Stop-PortIfBusy 5173

Write-Host ""
$backendDir  = "$root\backend\LocalCliRunner.Api"
$frontendDir = $root

$wtCmd = Get-Command wt -ErrorAction SilentlyContinue
$wtExe = if ($wtCmd) { $wtCmd.Source } else { $null }
if (-not $wtExe) {
    $wtExe = "$env:LOCALAPPDATA\Microsoft\WindowsApps\wt.exe"
    if (-not (Test-Path $wtExe)) { $wtExe = $null }
}

if ($wtExe) {
    Write-Host "[ OK ] Opening Windows Terminal (split pane)..." -ForegroundColor Green
    $wtArgs = "new-tab --title `"AI Spec - Backend`" --startingDirectory `"$backendDir`" cmd /k dotnet run ; split-pane -V --startingDirectory `"$frontendDir`" cmd /k npm run dev"
    Start-Process $wtExe -ArgumentList $wtArgs
} else {
    Write-Host "[ OK ] Starting servers in separate windows..." -ForegroundColor Green
    Start-Process cmd -ArgumentList "/k dotnet run" -WorkingDirectory $backendDir
    Start-Process cmd -ArgumentList "/k npm run dev" -WorkingDirectory $frontendDir
}

Write-Host ""
Write-Host "Done! Backend: http://127.0.0.1:5001  Frontend: http://127.0.0.1:5173" -ForegroundColor Cyan
Write-Host ""
