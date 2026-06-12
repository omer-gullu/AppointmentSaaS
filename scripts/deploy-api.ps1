param(
    [string]$ServerHost = "116.203.121.45",
    [string]$User = "root",
    [string]$SshKey = "$env:USERPROFILE\.ssh\id_ed25519",
    [string]$RemotePath = "/opt/appointmentsaas/api"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Push-Location $Root
try {
    dotnet publish Appointment_SaaS.API/Appointment_SaaS.API.csproj `
        -c Release `
        -o .\publish\api `
        --self-contained false

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    ssh -i $SshKey "${User}@${ServerHost}" "mkdir -p $RemotePath"
    scp -i $SshKey -r .\publish\api\* "${User}@${ServerHost}:${RemotePath}/"

    ssh -i $SshKey "${User}@${ServerHost}" @"
systemctl restart appointmentsaas-api 2>/dev/null || echo 'systemd service henuz yok — sunucuda api.env + systemd kurun'
"@
}
finally {
    Pop-Location
}

Write-Host "Deploy tamam: https://api.akillirandevu.net (Nginx kuruluysa)"
