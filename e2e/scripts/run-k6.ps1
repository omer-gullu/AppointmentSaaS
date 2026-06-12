# k6, Playwright gibi .env okumaz — bu script e2e/.env'i Process env'e yükler.
param(
  [Parameter(Mandatory = $true)]
  [string]$Script,

  # k6 run --env SCENARIO=... / E2E_LOAD_VUS=... (npm -- ayırıcısı PowerShell'de hata verir)
  [string]$Scenario = '',
  [int]$LoadVus = 0,

  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$K6Args
)

$ErrorActionPreference = 'Stop'
$e2eRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $e2eRoot '.env'

if (Test-Path $envFile) {
  Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -eq '' -or $line.StartsWith('#')) { return }
    $eq = $line.IndexOf('=')
    if ($eq -lt 1) { return }
    $name = $line.Substring(0, $eq).Trim()
    $value = $line.Substring($eq + 1).Trim()
    if ($value.StartsWith('"') -and $value.EndsWith('"')) {
      $value = $value.Substring(1, $value.Length - 2)
    }
    [Environment]::SetEnvironmentVariable($name, $value, 'Process')
  }
  Write-Host "[run-k6] .env yüklendi: $envFile"
} else {
  Write-Warning "[run-k6] .env bulunamadı: $envFile"
}

$scriptPath = Join-Path $e2eRoot $Script
if (-not (Test-Path $scriptPath)) {
  throw "k6 script bulunamadı: $scriptPath"
}

$k6EnvArgs = @()
if ($Scenario) {
  $k6EnvArgs += '--env', "SCENARIO=$Scenario"
}
if ($LoadVus -gt 0) {
  $k6EnvArgs += '--env', "E2E_LOAD_VUS=$LoadVus"
}

# npm " -- " ayırıcısı bazen buraya düşer; PowerShell bunu parametre sanır
$extra = @($K6Args | Where-Object { $_ -and $_ -ne '--' })

Set-Location $e2eRoot
& k6 run $scriptPath @k6EnvArgs @extra
