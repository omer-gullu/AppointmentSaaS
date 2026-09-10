param(
    [string]$ServerHost = "116.203.121.45",
    [string]$User = "root",
    [string]$SshKey = "$env:USERPROFILE\.ssh\id_ed25519",
    [string]$RemotePath = "/opt/appointmentsaas/webui"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Push-Location $Root
try {
    dotnet publish Appointment_SaaS.WebUI/Appointment_SaaS.WebUI.csproj `
        -c Release `
        -o .\publish\webui `
        --self-contained false

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    ssh -i $SshKey "${User}@${ServerHost}" "mkdir -p $RemotePath"
    scp -i $SshKey -r .\publish\webui\* "${User}@${ServerHost}:${RemotePath}/"

    ssh -i $SshKey "${User}@${ServerHost}" "mkdir -p /opt/appointmentsaas"
    $repoRoot = Split-Path -Parent $PSScriptRoot
    scp -i $SshKey (Join-Path $repoRoot "deploy\ensure-env.sh") "${User}@${ServerHost}:/tmp/appointmentsaas-ensure-env.sh"
    scp -i $SshKey (Join-Path $repoRoot "deploy\webui\env.example") "${User}@${ServerHost}:/tmp/appointmentsaas-webui.env.example"
    ssh -i $SshKey "${User}@${ServerHost}" @"
install -m 755 /tmp/appointmentsaas-ensure-env.sh /opt/appointmentsaas/ensure-env.sh
install -m 644 /tmp/appointmentsaas-webui.env.example /opt/appointmentsaas/webui.env.example
rm -f /tmp/appointmentsaas-ensure-env.sh /tmp/appointmentsaas-webui.env.example
bash /opt/appointmentsaas/ensure-env.sh webui /opt/appointmentsaas/webui.env.example
systemctl restart appointmentsaas-webui 2>/dev/null || echo 'systemd service henuz yok — sunucuda webui.env + systemd kurun'
"@
}
finally {
    Pop-Location
}

Write-Host "Deploy tamam: https://app.akillirandevu.net (Nginx kuruluysa)"
