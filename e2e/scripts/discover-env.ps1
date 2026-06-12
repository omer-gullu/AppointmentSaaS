# Yerel SQL'den E2E .env için örnek değerleri listeler (Manager + aktif tenant)

param(

    [string]$Server = "OMER\SQLEXPRESS",

    [string]$Database = "AppointmentSaaSDB",

    [switch]$ExportLoadTenants,

    [string]$LoadTenantsOut = ""

)



$query = @"

SET NOCOUNT ON;

SELECT TOP 5

  u.PhoneNumber AS E2E_MANAGER_PHONE,

  u.TenantID AS E2E_TENANT_ID,

  t.InstanceName AS E2E_INSTANCE_NAME,

  t.ApiKey AS E2E_N8N_TOKEN,

  t.SubscriptionReferenceCode AS E2E_SUBSCRIPTION_REF,

  (SELECT TOP 1 ServiceID FROM Services s WHERE s.TenantID = u.TenantID) AS E2E_SERVICE_ID,

  u.AppUserID AS E2E_STAFF_ID

FROM AppUsers u

INNER JOIN Tenants t ON u.TenantID = t.TenantID

INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId

INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id

WHERE oc.Name = 'Manager'

  AND u.Status = 1

  AND t.IsActive = 1

  AND t.IsSubscriptionActive = 1

  AND LEN(ISNULL(u.PhoneNumber,'')) >= 10

  AND t.InstanceName IS NOT NULL

ORDER BY u.AppUserID DESC;

"@

sqlcmd -S $Server -d $Database -E -Q $query -W -s "|"



Write-Host ""

Write-Host "Connection string for .env:"

Write-Host "E2E_DATABASE_URL=Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;"



$apiDevJson = Join-Path $PSScriptRoot "..\..\Appointment_SaaS.API\appsettings.Development.json"

if (Test-Path $apiDevJson) {

    try {

        $iyzico = (Get-Content $apiDevJson -Raw | ConvertFrom-Json).IyzicoSettings

        if ($iyzico.WebhookSecret) {

            Write-Host ""

            Write-Host "IYZICO_WEBHOOK_SECRET=$($iyzico.WebhookSecret)"

        }

    } catch {

        Write-Host "Iyzico WebhookSecret okunamadı: $apiDevJson" -ForegroundColor Yellow

    }

}



if ($ExportLoadTenants) {

    $loadQuery = @"

SET NOCOUNT ON;

SELECT TOP 100

  t.TenantID AS id,

  t.ApiKey AS token,

  t.InstanceName AS instanceName,

  (SELECT TOP 1 s.ServiceID FROM Services s WHERE s.TenantID = t.TenantID ORDER BY s.ServiceID) AS serviceId,

  (SELECT TOP 1 u.AppUserID FROM AppUsers u

   INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId

   INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id

   WHERE u.TenantID = t.TenantID AND oc.Name = 'Manager'

   ORDER BY u.AppUserID) AS staffId

FROM Tenants t

WHERE t.IsActive = 1

  AND t.IsSubscriptionActive = 1

  AND t.InstanceName IS NOT NULL

  AND LEN(ISNULL(t.ApiKey,'')) > 8

ORDER BY t.TenantID;

"@



    $raw = sqlcmd -S $Server -d $Database -E -Q $loadQuery -W -h -1 -s "|"

    $tenants = @()

    foreach ($line in $raw) {

        if ([string]::IsNullOrWhiteSpace($line)) { continue }

        if ($line -match '^\(\d+ rows affected\)' -or $line -match '^-+$') { continue }

        $parts = $line -split '\|'

        if ($parts.Count -lt 5) { continue }

        $id = [int]($parts[0].Trim())

        $token = $parts[1].Trim()

        $instanceName = $parts[2].Trim()

        $serviceId = [int]($parts[3].Trim())

        $staffId = [int]($parts[4].Trim())

        if ($id -le 0 -or $staffId -le 0 -or $serviceId -le 0) { continue }

        $tenants += [ordered]@{

            id           = $id

            token        = $token

            instanceName = $instanceName

            serviceId    = $serviceId

            staffId      = $staffId

        }

    }



    $outPath = $LoadTenantsOut

    if ([string]::IsNullOrWhiteSpace($outPath)) {

        $outPath = Join-Path $PSScriptRoot "..\fixtures\load-tenants.json"

    }

    $dir = Split-Path $outPath -Parent

    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    $payload = @{ tenants = $tenants }

    $payload | ConvertTo-Json -Depth 4 | Set-Content -Path $outPath -Encoding UTF8



    Write-Host ""

    Write-Host "K6 load tenants ($($tenants.Count) kayıt) -> $outPath" -ForegroundColor Green

    Write-Host "E2E_LOAD_TENANTS_JSON=fixtures/load-tenants.json"

}

# ── Güvenlik testleri (security.spec.ts) ──
Write-Host ""
Write-Host "=== Güvenlik testleri (.env) ===" -ForegroundColor Cyan

$secQuery = @"
SET NOCOUNT ON;
SELECT
  a.TenantID AS TenantAId,
  a.ApiKey AS TenantAToken,
  b.TenantID AS TenantBId,
  b.ApiKey AS TenantBToken,
  (SELECT TOP 1 AppointmentID FROM Appointments ap WHERE ap.TenantID = b.TenantID ORDER BY ap.AppointmentID DESC) AS TenantBAppointmentId,
  p.TenantID AS PassiveTenantId,
  p.ApiKey AS PassiveTenantToken,
  p.InstanceName AS PassiveInstanceName,
  pm.PhoneNumber AS PassiveManagerPhone
FROM (
  SELECT TOP 1 t.TenantID, t.ApiKey
  FROM Tenants t
  WHERE t.IsActive = 1 AND t.IsSubscriptionActive = 1 AND LEN(ISNULL(t.ApiKey,'')) > 8
  ORDER BY t.TenantID
) a
CROSS JOIN (
  SELECT TOP 1 t.TenantID, t.ApiKey
  FROM Tenants t
  WHERE t.IsActive = 1 AND t.IsSubscriptionActive = 1 AND LEN(ISNULL(t.ApiKey,'')) > 8
  ORDER BY t.TenantID DESC
) b
OUTER APPLY (
  SELECT TOP 1 t.TenantID, t.ApiKey, t.InstanceName
  FROM Tenants t
  WHERE (t.IsSubscriptionActive = 0 OR t.IsActive = 0) AND t.InstanceName IS NOT NULL
  ORDER BY t.TenantID DESC
) p
OUTER APPLY (
  SELECT TOP 1 u.PhoneNumber
  FROM AppUsers u
  INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId
  INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id
  WHERE u.TenantID = p.TenantID AND oc.Name = 'Manager' AND LEN(ISNULL(u.PhoneNumber,'')) >= 10
) pm;
"@

try {
    $secRaw = sqlcmd -S $Server -d $Database -E -Q $secQuery -W -h -1 -s "|"
    $secLine = $secRaw | Where-Object { $_ -match '\|' -and $_ -notmatch '^-+$' } | Select-Object -First 1
    if ($secLine) {
        $p = $secLine -split '\|'
        if ($p.Count -ge 8) {
            Write-Host "E2E_TENANT_A_ID=$($p[0].Trim())"
            Write-Host "E2E_TENANT_A_TOKEN=$($p[1].Trim())"
            Write-Host "E2E_TENANT_B_ID=$($p[2].Trim())"
            Write-Host "E2E_TENANT_B_TOKEN=$($p[3].Trim())"
            Write-Host "E2E_TENANT_B_APPOINTMENT_ID=$($p[4].Trim())"
            Write-Host "E2E_PASSIVE_TENANT_ID=$($p[5].Trim())"
            Write-Host "E2E_PASSIVE_TENANT_TOKEN=$($p[6].Trim())"
            Write-Host "E2E_PASSIVE_INSTANCE_NAME=$($p[7].Trim())"
            Write-Host "E2E_PASSIVE_MANAGER_PHONE=$($p[8].Trim())"
        }
    } else {
        Write-Host "Guvenlik tenant sorgusu sonuc dondurmedi. sync-env.mjs calistirin." -ForegroundColor Yellow
    }
} catch {
    Write-Host "Guvenlik env sorgusu calistirilamadi: $_" -ForegroundColor Yellow
}

