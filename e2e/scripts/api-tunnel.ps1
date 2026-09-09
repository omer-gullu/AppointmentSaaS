# API on the server listens on :5294 (not public). This forwards laptop :5294
# so Playwright can reach it. Usage from e2e/:  npm run tunnel:api
# Then:  E2E_API_URL=http://127.0.0.1:5294
#
# Windows OpenSSH often dies with "client_loop: send disconnect: Connection reset"
# unless IPQoS=none. The loop reconnects if the session still drops.

param(
    [string]$ServerHost = "116.203.121.45",
    [string]$User = "root",
    [string]$SshKey = "$env:USERPROFILE\.ssh\id_ed25519",
    [int]$LocalPort = 5294,
    [int]$RemotePort = 5294,
    [switch]$KillLocalListener
)

$ErrorActionPreference = "Stop"

function Test-LocalApi {
    try {
        Invoke-WebRequest -Uri "http://127.0.0.1:$LocalPort/" -UseBasicParsing -TimeoutSec 3 | Out-Null
        return $true
    } catch {
        if ($_.Exception.Response) { return $true }
        return $false
    }
}

function Get-ListenPids {
    Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
}

function Stop-ListenPids {
    $pids = @(Get-ListenPids)
    foreach ($procId in $pids) {
        if ($procId -and $procId -ne 0) {
            Write-Host "Stopping PID $procId on port $LocalPort"
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Seconds 1
}

$listening = @(Get-ListenPids)
if ($listening.Count -gt 0) {
    if ($KillLocalListener) {
        Stop-ListenPids
    } elseif (Test-LocalApi) {
        Write-Host "localhost:$LocalPort already responds. Tunnel may already be open."
        Write-Host "Playwright: E2E_API_URL=http://127.0.0.1:$LocalPort"
        exit 0
    } else {
        Write-Host "Port $LocalPort is busy. Stopping the local listener..."
        Stop-ListenPids
    }
}

if (-not (Test-Path $SshKey)) {
    Write-Error "SSH key not found: $SshKey"
}

Write-Host "SSH tunnel: localhost:$LocalPort -> ${ServerHost}:127.0.0.1:$RemotePort"
Write-Host "Playwright: E2E_API_URL=http://127.0.0.1:$LocalPort"
Write-Host "Keep this window open. Press Ctrl+C to close."
Write-Host ""

$sshArgs = @(
    "-i", $SshKey,
    "-N",
    "-o", "BatchMode=yes",
    "-o", "IdentitiesOnly=yes",
    "-o", "ExitOnForwardFailure=yes",
    "-o", "ServerAliveInterval=15",
    "-o", "ServerAliveCountMax=4",
    "-o", "TCPKeepAlive=yes",
    "-o", "IPQoS=none",
    "-L", "${LocalPort}:127.0.0.1:${RemotePort}",
    "${User}@${ServerHost}"
)

while ($true) {
    & ssh @sshArgs
    $code = $LASTEXITCODE
    Write-Host ""
    Write-Host "Tunnel dropped (exit $code). Reconnecting in 3s..."
    Stop-ListenPids
    Start-Sleep -Seconds 3
}
