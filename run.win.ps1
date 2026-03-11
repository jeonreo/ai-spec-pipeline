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

# --- appsettings.json 읽어서 runner 결정 ---
$appSettingsPath = "$root\backend\LocalCliRunner.Api\appsettings.json"
$vertexProjectId = $null
if (Test-Path $appSettingsPath) {
    try {
        $cfg = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        $vertexProjectId = $cfg.Vertex.ProjectId
    } catch {
        Write-Host "[WARN] appsettings.json parse failed - defaulting to Claude CLI" -ForegroundColor Yellow
    }
}

$useVertex = -not [string]::IsNullOrWhiteSpace($vertexProjectId)

# --- 공통 도구 체크 ---
Check-Tool "dotnet" "https://dot.net (.NET 10 SDK)"
Check-Tool "node"   "https://nodejs.org (LTS)"

# --- Runner별 체크 ---
Write-Host ""
if ($useVertex) {
    Write-Host "[ .. ] Runner: Vertex AI (ProjectId: $vertexProjectId)" -ForegroundColor Cyan

    # gcloud CLI 설치 확인
    if (-not (Get-Command gcloud -ErrorAction SilentlyContinue)) {
        Write-Host "[FAIL] gcloud CLI not found" -ForegroundColor Red
        Write-Host "       Install: https://cloud.google.com/sdk/docs/install"
        Read-Host "`nPress Enter to exit"; exit 1
    }
    try { $gver = & gcloud --version 2>$null | Select-Object -First 1 } catch { $gver = "?" }
    Write-Host "[ OK ] $("gcloud".PadRight(8)): $gver" -ForegroundColor Green

    # ADC(Application Default Credentials) 확인
    Write-Host "[ .. ] Checking gcloud ADC auth..."
    $token = & gcloud auth application-default print-access-token 2>$null
    if (-not $token) {
        Write-Host "[WARN] ADC credentials not found. Run the following command:" -ForegroundColor Yellow
        Write-Host "       gcloud auth application-default login" -ForegroundColor Yellow
        Write-Host "       Then restart this script."
        Read-Host "`nPress Enter to exit"; exit 1
    }
    Write-Host "[ OK ] gcloud ADC auth OK" -ForegroundColor Green

} else {
    Write-Host "[ .. ] Runner: Claude CLI (local)" -ForegroundColor Cyan
    Check-Tool "claude" "https://docs.anthropic.com/en/docs/claude-code"

    # Claude 로그인 상태 확인
    Write-Host "[ .. ] Checking claude auth..."
    $claudeStatus = & claude --print "ping" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $claudeStatus) {
        Write-Host "[WARN] Claude auth may not be configured." -ForegroundColor Yellow
        Write-Host "       Run 'claude login' and try again." -ForegroundColor Yellow
    } else {
        Write-Host "[ OK ] claude auth OK" -ForegroundColor Green
    }
}
Write-Host ""

# --- Jira API Token 체크 (.env 파일 → 환경변수 순으로 확인) ---
$envFilePath = "$root\.env"

# .env 파일 파싱 함수
function Get-EnvValue($path, $key) {
    if (-not (Test-Path $path)) { return $null }
    foreach ($line in Get-Content $path) {
        if ($line -match "^\s*$key\s*=\s*(.+)$") { return $Matches[1].Trim() }
    }
    return $null
}

# .env → 세션 env var → appsettings 순서로 토큰 확인
$jiraToken = Get-EnvValue $envFilePath "Jira__ApiToken"
if ([string]::IsNullOrWhiteSpace($jiraToken)) { $jiraToken = $env:Jira__ApiToken }

# appsettings에서 BaseUrl/Email 읽기 (표시용)
$jiraBaseUrl = $null; $jiraEmail = $null
if (Test-Path $appSettingsPath) {
    try {
        $cfg = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        $jiraBaseUrl = $cfg.Jira.BaseUrl
        $jiraEmail   = $cfg.Jira.Email
    } catch {}
}

if ([string]::IsNullOrWhiteSpace($jiraToken)) {
    Write-Host "[WARN] Jira API Token is not configured." -ForegroundColor Yellow
    Write-Host "       BaseUrl : $jiraBaseUrl"
    Write-Host "       Email   : $jiraEmail"
    Write-Host ""
    Write-Host "  Generate token: https://id.atlassian.com/manage-profile/security/api-tokens" -ForegroundColor Cyan
    Write-Host ""
    $inputToken = Read-Host "  Enter API Token (press Enter to skip)"

    if (-not [string]::IsNullOrWhiteSpace($inputToken)) {
        $inputToken = $inputToken.Trim()
        # .env 파일에 저장 (없으면 생성)
        $envLine = "Jira__ApiToken=$inputToken"
        if (Test-Path $envFilePath) {
            $lines = Get-Content $envFilePath | Where-Object { $_ -notmatch "^\s*Jira__ApiToken\s*=" }
            ($lines + $envLine) | Set-Content $envFilePath -Encoding UTF8
        } else {
            $envLine | Set-Content $envFilePath -Encoding UTF8
        }
        $env:Jira__ApiToken = $inputToken
        Write-Host "[ OK ] Jira API Token saved to .env" -ForegroundColor Green
    } else {
        Write-Host "[ -- ] Jira Token skipped (Jira integration will not work)" -ForegroundColor DarkYellow
    }
} else {
    # .env에서 읽은 토큰을 현재 세션 env var로 주입 (백엔드 프로세스가 상속)
    $env:Jira__ApiToken = $jiraToken
    $maskedToken = $jiraToken.Substring(0, [Math]::Min(8, $jiraToken.Length)) + "****"
    Write-Host "[ OK ] $("Jira".PadRight(8)): Token OK ($maskedToken)" -ForegroundColor Green
}
Write-Host ""

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
