/**
 * DB'den E2E .env degerlerini okur / eksik guvenlik alanlarini tamamlar.
 * Kullanim: node scripts/sync-env.mjs
 */
import { execSync } from 'child_process';
import { readFileSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import dotenv from 'dotenv';

const __dirname = dirname(fileURLToPath(import.meta.url));
const envPath = join(__dirname, '..', '.env');
dotenv.config({ path: envPath });

const dbServer = process.env.E2E_DB_SERVER?.trim() || 'OMER';
const dbInstance = process.env.E2E_DB_INSTANCE?.trim() || 'SQLEXPRESS';
const dbName = process.env.E2E_DB_NAME?.trim() || 'AppointmentSaaSDB';
const sqlServer = `${dbServer}\\${dbInstance}`;

function sqlQuery(query) {
  const oneLine = query.replace(/\s+/g, ' ').trim();
  const q = oneLine.replace(/"/g, '""');
  const cmd = `sqlcmd -S "${sqlServer}" -d "${dbName}" -E -Q "${q}" -h-1 -W`;
  return execSync(cmd, { encoding: 'utf8', timeout: 120_000, windowsHide: true });
}

function parseJsonLine(out) {
  const text = String(out ?? '');
  const match = text.match(/\{[\s\S]*\}/);
  if (!match) return null;
  try {
    return JSON.parse(match[0]);
  } catch {
    return null;
  }
}

function parseIntLine(out) {
  const line = out
    .split(/\r?\n/)
    .map((l) => l.trim())
    .find((l) => /^\d+$/.test(l));
  return line ? Number(line) : null;
}

function mergeEnvFile(updates) {
  const raw = readFileSync(envPath, 'utf8');
  const lines = raw.split(/\r?\n/);
  const keys = new Set(Object.keys(updates));
  const seen = new Set();
  const out = [];

  for (const line of lines) {
    const m = line.match(/^([A-Z0-9_]+)=/);
    if (m && keys.has(m[1])) {
      out.push(`${m[1]}=${updates[m[1]]}`);
      seen.add(m[1]);
      continue;
    }
    out.push(line);
  }

  const missing = Object.entries(updates).filter(([k]) => !seen.has(k));
  if (missing.length > 0) {
    if (out.length && out[out.length - 1] !== '') out.push('');
    out.push('# Guvenlik testleri (sync-env.mjs)');
    for (const [k, v] of missing) out.push(`${k}=${v}`);
  }

  writeFileSync(envPath, out.join('\n').replace(/\n+$/, '\n'), 'utf8');
}

async function ensureTenantBE2eReady(tenantId) {
  const open = '08:00:00';
  const close = '21:00:00';
  const inserts = [];
  for (let day = 0; day < 7; day++) {
    const closed = day === 0 ? 1 : 0;
    const op = day === 0 ? '00:00:00' : open;
    const cl = day === 0 ? '00:00:00' : close;
    inserts.push(
      `INSERT INTO BusinessHours (TenantID, DayOfWeek, OpenTime, CloseTime, IsClosed) VALUES (${tenantId}, ${day}, '${op}', '${cl}', ${closed});`,
    );
  }
  sqlQuery(
    `SET NOCOUNT ON; DELETE FROM BusinessHours WHERE TenantID = ${tenantId}; ${inserts.join(' ')} UPDATE Tenants SET BreakTimeEnabled = 1, BreakStartTime = '12:00:00', BreakEndTime = '13:00:00' WHERE TenantID = ${tenantId};`,
  );
}

async function createTenantBAppointmentViaSql(tenantB) {
  const d = new Date();
  d.setDate(d.getDate() + 21);
  d.setHours(11, 0, 0, 0);
  const pad = (n) => String(n).padStart(2, '0');
  const start = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
  const endD = new Date(d.getTime() + 30 * 60_000);
  const end = `${endD.getFullYear()}-${pad(endD.getMonth() + 1)}-${pad(endD.getDate())} ${pad(endD.getHours())}:${pad(endD.getMinutes())}:00`;
  const phone = `5320000${String(tenantB.tenantId).padStart(3, '0')}`.slice(0, 11);

  sqlQuery(`
SET NOCOUNT ON;
INSERT INTO Appointments (CustomerName, CustomerPhone, StartDate, EndDate, Status, Note, IsConfirmed, TenantID, ServiceID, AppUserID)
VALUES (N'Esse Security Sync', N'${phone}', '${start}', '${end}', N'Pending', N'E2E security', 0, ${tenantB.tenantId}, ${tenantB.serviceId}, ${tenantB.staffId});
SELECT TOP 1 AppointmentID FROM Appointments WHERE TenantID = ${tenantB.tenantId} ORDER BY AppointmentID DESC`);
  return parseIntLine(
    sqlQuery(
      `SET NOCOUNT ON; SELECT TOP 1 AppointmentID FROM Appointments WHERE TenantID = ${tenantB.tenantId} ORDER BY AppointmentID DESC`,
    ),
  );
}

async function createTenantBAppointment(tenantB) {
  const apiBase = process.env.E2E_API_URL?.trim() || 'http://localhost:5294';
  const d = new Date();
  d.setDate(d.getDate() + 21);
  d.setHours(11, 0, 0, 0);
  const pad = (n) => String(n).padStart(2, '0');
  const iso = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
  const phone = `5320000${String(tenantB.tenantId).padStart(3, '0')}`.slice(0, 11);
  const qs = new URLSearchParams({ instanceName: tenantB.instanceName });
  const url = `${apiBase}/api/Appointments?${qs}`;

  try {
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Auth-Token': tenantB.apiKey,
        'X-Tenant-Id': String(tenantB.tenantId),
      },
      body: JSON.stringify({
        customerName: 'Esse Security Sync',
        customerPhone: phone,
        businessPhone: tenantB.instanceName,
        serviceID: tenantB.serviceId,
        appUserID: tenantB.staffId,
        startDate: iso,
      }),
      signal: AbortSignal.timeout(15_000),
    });

    const json = await res.json().catch(() => ({}));
    if (res.ok) {
      const id = Number(json.ID ?? json.id ?? 0);
      if (id > 0) return id;
    }
    console.log(`API randevu olusturamadi (${res.status}) — SQL ile deneniyor…`);
  } catch (e) {
    console.log(`API ulasilamadi (${e.message}) — SQL ile deneniyor…`);
  }

  return createTenantBAppointmentViaSql(tenantB);
}

async function main() {
  const tenantAId = Number(process.env.E2E_TENANT_ID ?? 0);
  const tenantAToken =
    process.env.E2E_N8N_TOKEN?.trim() || process.env.E2E_TENANT_A_TOKEN?.trim() || '';
  if (tenantAId <= 0 || !tenantAToken) {
    console.error('E2E_TENANT_ID ve E2E_N8N_TOKEN gerekli. Once discover-env.ps1 calistirin.');
    process.exit(1);
  }

  const tenantAStaff = parseJsonLine(
    sqlQuery(`
SET NOCOUNT ON;
SELECT TOP 1
  u.AppUserID AS staffId,
  u.PhoneNumber AS managerPhone
FROM AppUsers u
INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId
INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id
WHERE u.TenantID = ${tenantAId}
  AND oc.Name = 'Manager'
  AND u.Status = 1
  AND LEN(ISNULL(u.PhoneNumber,'')) >= 10
ORDER BY u.AppUserID DESC
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`),
  );
  if (!tenantAStaff?.staffId || !tenantAStaff?.managerPhone) {
    console.error(`Tenant A (${tenantAId}) icin aktif Manager personel bulunamadi.`);
    process.exit(1);
  }
  const aDigits = String(tenantAStaff.managerPhone).replace(/\D/g, '');
  const managerPhoneA =
    aDigits.length >= 10 ? (aDigits.length === 10 ? `0${aDigits}` : `0${aDigits.slice(-10)}`) : '';

  const tenantBQ = `
SET NOCOUNT ON;
SELECT TOP 1
  t.TenantID AS tenantId,
  t.ApiKey AS apiKey,
  t.InstanceName AS instanceName,
  (SELECT TOP 1 s.ServiceID FROM Services s WHERE s.TenantID = t.TenantID ORDER BY s.ServiceID) AS serviceId,
  (SELECT TOP 1 u.AppUserID FROM AppUsers u
   INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId
   INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id
   WHERE u.TenantID = t.TenantID AND oc.Name = 'Manager' AND u.Status = 1 ORDER BY u.AppUserID DESC) AS staffId
FROM Tenants t
WHERE t.TenantID <> ${tenantAId}
  AND t.IsActive = 1
  AND t.IsSubscriptionActive = 1
  AND LEN(ISNULL(t.ApiKey,'')) > 8
  AND t.InstanceName IS NOT NULL
  AND EXISTS (
    SELECT 1 FROM AppUsers u
    INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId
    INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id
    WHERE u.TenantID = t.TenantID AND oc.Name = 'Manager'
  )
ORDER BY t.TenantID DESC
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;

  const tenantB = parseJsonLine(sqlQuery(tenantBQ));
  if (!tenantB?.tenantId || !tenantB.staffId) {
    console.error('Ikinci aktif tenant bulunamadi (ApiKey + instance + manager personel).');
    process.exit(1);
  }

  if (!tenantB.serviceId) {
    console.log(`Tenant B (${tenantB.tenantId}) hizmet yok — E2E test hizmeti ekleniyor…`);
    sqlQuery(`
SET NOCOUNT ON;
INSERT INTO Services (TenantID, Name, Price, DurationInMinutes)
VALUES (${tenantB.tenantId}, N'E2E Guvenlik Hizmeti', 0, 30);`);
    const svc = parseJsonLine(
      sqlQuery(
        `SET NOCOUNT ON; SELECT TOP 1 ServiceID AS serviceId FROM Services WHERE TenantID = ${tenantB.tenantId} ORDER BY ServiceID DESC FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`,
      ),
    );
    tenantB.serviceId = svc?.serviceId;
  }

  if (!tenantB.serviceId) {
    console.error('Tenant B icin hizmet olusturulamadi.');
    process.exit(1);
  }

  let apptQ = `SET NOCOUNT ON; SELECT TOP 1 AppointmentID FROM Appointments WHERE TenantID = ${tenantB.tenantId} ORDER BY AppointmentID DESC`;
  let appointmentId = parseIntLine(sqlQuery(apptQ));
  if (!appointmentId) {
    console.log(`Tenant B (${tenantB.tenantId}) icin randevu yok — olusturuluyor…`);
    await ensureTenantBE2eReady(tenantB.tenantId);
    appointmentId = await createTenantBAppointment(tenantB);
  }
  if (!appointmentId) {
    console.error('Tenant B randevusu olusturulamadi.');
    process.exit(1);
  }

  const passivePhoneRow = parseJsonLine(
    sqlQuery(
      `SET NOCOUNT ON; SELECT TOP 1 u.PhoneNumber AS phone FROM AppUsers u INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id WHERE u.TenantID = ${tenantB.tenantId} AND oc.Name = 'Manager' AND u.Status = 1 AND LEN(ISNULL(u.PhoneNumber,'')) >= 10 ORDER BY u.AppUserID DESC FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`,
    ),
  );
  const digits = String(passivePhoneRow?.phone ?? '').replace(/\D/g, '');
  const passivePhone =
    digits.length >= 10 ? (digits.length === 10 ? `0${digits}` : `0${digits.slice(-10)}`) : '';
  if (!passivePhone) {
    console.error(`Tenant B (${tenantB.tenantId}) manager telefonu bulunamadi.`);
    process.exit(1);
  }

  const updates = {
    E2E_STAFF_ID: String(tenantAStaff.staffId),
    E2E_MANAGER_PHONE: managerPhoneA,
    E2E_TENANT_A_ID: String(tenantAId),
    E2E_TENANT_A_TOKEN: tenantAToken,
    E2E_TENANT_B_ID: String(tenantB.tenantId),
    E2E_TENANT_B_TOKEN: String(tenantB.apiKey).trim(),
    E2E_TENANT_B_APPOINTMENT_ID: String(appointmentId),
    E2E_PASSIVE_TENANT_ID: String(tenantB.tenantId),
    E2E_PASSIVE_TENANT_TOKEN: String(tenantB.apiKey).trim(),
    E2E_PASSIVE_INSTANCE_NAME: String(tenantB.instanceName).trim(),
    E2E_PASSIVE_MANAGER_PHONE: passivePhone,
  };

  mergeEnvFile(updates);
  console.log('e2e/.env guncellendi:');
  for (const [k, v] of Object.entries(updates)) {
    const masked = k.includes('TOKEN') ? `${v.slice(0, 8)}…` : v;
    console.log(`  ${k}=${masked}`);
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
