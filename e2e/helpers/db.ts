import { execSync } from 'child_process';
import fs from 'fs';
import path from 'path';
import sql from 'mssql';
import { requireEnv, hasEnv, isReadonlyEnv } from './env';

let cachedSqlCmdExe: string | null = null;

/** Playwright/npm PATH'te sqlcmd olmayabilir; tam yolu bul veya E2E_SQLCMD_PATH kullan. */
function resolveSqlCmdExecutable(): string {
  if (cachedSqlCmdExe) return cachedSqlCmdExe;

  const explicit = process.env.E2E_SQLCMD_PATH?.trim();
  if (explicit) {
    cachedSqlCmdExe = explicit.includes(' ') ? `"${explicit}"` : explicit;
    return cachedSqlCmdExe;
  }

  if (process.platform === 'win32') {
    const roots = [process.env['ProgramFiles'], process.env['ProgramFiles(x86)']].filter(
      Boolean,
    ) as string[];
    const relPaths = [
      'Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\sqlcmd.exe',
      'Microsoft SQL Server\\Client SDK\\ODBC\\130\\Tools\\Binn\\sqlcmd.exe',
      'Microsoft SQL Server\\150\\Tools\\Binn\\sqlcmd.exe',
      'Microsoft SQL Server\\160\\Tools\\Binn\\sqlcmd.exe',
      'Microsoft SQL Server\\110\\Tools\\Binn\\sqlcmd.exe',
    ];
    for (const root of roots) {
      for (const rel of relPaths) {
        const full = path.join(root, rel);
        if (fs.existsSync(full)) {
          cachedSqlCmdExe = `"${full}"`;
          return cachedSqlCmdExe;
        }
      }
    }
  }

  cachedSqlCmdExe = 'sqlcmd';
  return cachedSqlCmdExe;
}

function sqlCmdServer(): string {
  const host = process.env.E2E_DB_SERVER?.trim() ?? 'OMER';
  const instance = process.env.E2E_DB_INSTANCE?.trim();
  return instance ? `${host}\\${instance}` : host;
}

/** Windows: sqlcmd named instance ile çalışır (mssql sıklıkla Browser/port hatası verir). */
function getOtpForPhoneViaSqlCmd(phoneNumber: string): string | null {
  const digits = phoneNumber.replace(/\D/g, '').slice(-10);
  if (digits.length < 10) return null;
  const query = `SET NOCOUNT ON; SELECT TOP 1 OtpCode FROM AppUsers WHERE PhoneNumber LIKE '%${digits}%' AND (OtpExpiry IS NULL OR OtpExpiry > GETDATE()) ORDER BY AppUserID DESC`;
  const out = runSqlCmdQuery(query);
  const line = out
    .split(/\r?\n/)
    .map((l) => l.trim())
    .find((l) => /^\d{6}$/.test(l));
  return line ?? null;
}

function useSqlCmd(): boolean {
  if (process.env.E2E_USE_SQLCMD === 'false') return false;
  if (process.env.E2E_OTP_USE_SQLCMD === 'false') return false;
  if (process.env.E2E_USE_SQLCMD === 'true' || process.env.E2E_OTP_USE_SQLCMD === 'true') return true;
  return process.platform === 'win32';
}

const SQLCMD_TIMEOUT_MS = Number(process.env.E2E_SQLCMD_TIMEOUT_MS ?? 60_000);

function runSqlCmdQuery(query: string): string {
  const db = process.env.E2E_DB_NAME?.trim() ?? 'AppointmentSaaSDB';
  const oneLine = query.replace(/\s+/g, ' ').trim();
  const exe = resolveSqlCmdExecutable();
  const cmd = `${exe} -S "${sqlCmdServer()}" -d "${db}" -E -Q "${oneLine.replace(/"/g, '""')}" -h-1 -W`;
  try {
    return execSync(cmd, { encoding: 'utf8', timeout: SQLCMD_TIMEOUT_MS, windowsHide: true });
  } catch (e) {
    const msg = String((e as Error).message ?? e);
    if (/ETIMEDOUT|timed out/i.test(msg)) {
      throw new Error(
        `sqlcmd zaman aşımı (${SQLCMD_TIMEOUT_MS}ms). E2E_SQLCMD_PATH ile tam yolu verin veya SQL Server'ı kontrol edin.`,
      );
    }
    if (/is not recognized|ENOENT|not found/i.test(msg)) {
      throw new Error(
        `sqlcmd bulunamadı (${exe}). E2E_SQLCMD_PATH=C:\\...\\sqlcmd.exe ekleyin veya SQL Server Command Line Tools kurun.`,
      );
    }
    throw e;
  }
}

function isSqlCmdTimeoutError(err: unknown): boolean {
  return /ETIMEDOUT|zaman aşımı|timed out/i.test(String((err as Error)?.message ?? err));
}

function findAppointmentByPhoneSqlCmd(tenantId: number, phoneFragment: string): AppointmentRow | null {
  const digits = phoneFragment.replace(/\D/g, '').slice(-10);
  const q = `SET NOCOUNT ON; SELECT TOP 1 AppointmentID, CustomerName FROM Appointments WHERE TenantID = ${tenantId} AND CustomerPhone LIKE '%${digits}%' ORDER BY AppointmentID DESC`;
  const out = runSqlCmdQuery(q);
  const line = out
    .split(/\r?\n/)
    .map((l) => l.trim())
    .find((l) => /^\d+\s+\S/.test(l));
  if (!line) return null;
  const m = line.match(/^(\d+)\s+(.+)$/);
  if (!m) return null;
  return {
    AppointmentID: Number(m[1]),
    TenantID: tenantId,
    CustomerName: m[2].trim(),
    CustomerPhone: phoneFragment,
    GoogleEventID: null,
    Status: 'Pending',
  };
}

let pool: sql.ConnectionPool | null = null;

/** Named instance (SQLEXPRESS) — ham connection string 1433’e düşmesin diye config objesi. */
function buildSqlConfig(): sql.config {
  const server = process.env.E2E_DB_SERVER?.trim();
  const instance = process.env.E2E_DB_INSTANCE?.trim();
  const database = process.env.E2E_DB_NAME?.trim() ?? 'AppointmentSaaSDB';

  if (server) {
    return {
      server,
      database,
      options: {
        instanceName: instance || undefined,
        encrypt: false,
        trustServerCertificate: true,
      },
      authentication: { type: 'default', options: {} },
    };
  }

  const connectionString =
    process.env.E2E_DATABASE_URL?.trim() ||
    process.env.ConnectionStrings__DefaultConnection?.trim();

  if (!connectionString) {
    throw new Error(
      'E2E_DB_SERVER + E2E_DB_INSTANCE veya E2E_DATABASE_URL gerekli. DB’siz OTP: E2E_OTP_CODE=123456',
    );
  }

  const parts = Object.fromEntries(
    connectionString.split(';').filter(Boolean).map((part) => {
      const eq = part.indexOf('=');
      const key = (eq === -1 ? part : part.slice(0, eq)).trim().toLowerCase();
      const value = eq === -1 ? '' : part.slice(eq + 1).trim();
      return [key, value];
    }),
  );

  let host = parts.server ?? 'localhost';
  let instanceName = instance;

  if (host.includes('\\')) {
    const [h, inst] = host.split('\\');
    host = h;
    instanceName = inst;
  }

  return {
    server: host,
    database: parts.database ?? database,
    options: {
      instanceName: instanceName || undefined,
      encrypt: false,
      trustServerCertificate: true,
    },
    authentication: { type: 'default', options: {} },
  };
}

export async function getDbPool(): Promise<sql.ConnectionPool> {
  if (pool?.connected) return pool;

  try {
    pool = await sql.connect(buildSqlConfig());
    return pool;
  } catch (e) {
    const hint = useSqlCmd()
      ? " Windows'ta sqlcmd kullanın (varsayılan): E2E_SQLCMD_PATH veya SQL Server Tools. mssql driver SQLEXPRESS için SQL Browser gerekir. DB'siz OTP: E2E_OTP_CODE=123456."
      : " SQL Server çalışıyor mu? E2E_DB_SERVER / E2E_DB_INSTANCE — veya E2E_OTP_CODE=123456.";
    throw new Error(`${(e as Error).message}.${hint}`);
  }
}

export async function closeDbPool(): Promise<void> {
  if (pool) {
    await pool.close();
    pool = null;
  }
}

export function assertDbWritable(): void {
  if (isReadonlyEnv()) {
    throw new Error('DB mutations are disabled in production (readonly) environment.');
  }
}

export interface AppointmentRow {
  AppointmentID: number;
  TenantID: number;
  CustomerName: string;
  CustomerPhone: string;
  GoogleEventID: string | null;
  Status: string;
}

export interface AppointmentDetailRow extends AppointmentRow {
  ServiceID: number;
  AppUserID: number;
  StartDate: Date;
}

export async function findAppointmentByCustomerPhone(
  tenantId: number,
  phoneFragment: string,
): Promise<AppointmentRow | null> {
  if (useSqlCmd()) {
    try {
      return findAppointmentByPhoneSqlCmd(tenantId, phoneFragment);
    } catch {
      /* mssql fallback */
    }
  }

  const db = await getDbPool();
  const result = await db
    .request()
    .input('tenantId', sql.Int, tenantId)
    .input('phone', sql.NVarChar, `%${phoneFragment}%`)
    .query<AppointmentRow>(`
      SELECT TOP 1 AppointmentID, TenantID, CustomerName, CustomerPhone, GoogleEventID, Status
      FROM Appointments
      WHERE TenantID = @tenantId AND CustomerPhone LIKE @phone
      ORDER BY AppointmentID DESC
    `);
  return result.recordset[0] ?? null;
}

export async function waitForAppointment(
  tenantId: number,
  phoneFragment: string,
  timeoutMs = 30_000,
): Promise<AppointmentRow> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const row = await findAppointmentByCustomerPhone(tenantId, phoneFragment);
    if (row) return row;
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`Appointment not found for tenant ${tenantId}, phone ~${phoneFragment}`);
}

export async function getAppointmentById(appointmentId: number): Promise<AppointmentDetailRow | null> {
  const jsonQuery = `SET NOCOUNT ON; SELECT TOP 1 AppointmentID, TenantID, CustomerName, CustomerPhone, GoogleEventID, Status, ServiceID, AppUserID, CONVERT(varchar(19), StartDate, 126) AS StartDate FROM Appointments WHERE AppointmentID = ${appointmentId} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;
  if (useSqlCmd()) {
    try {
      const out = runSqlCmdQuery(jsonQuery);
      const jsonLine = out
        .split(/\r?\n/)
        .map((l) => l.trim())
        .find((l) => l.startsWith('{'));
      if (jsonLine) {
        const o = JSON.parse(jsonLine) as {
          AppointmentID: number;
          TenantID: number;
          CustomerName: string;
          CustomerPhone: string;
          GoogleEventID: string | null;
          Status: string;
          ServiceID: number;
          AppUserID: number;
          StartDate: string;
        };
        return {
          AppointmentID: o.AppointmentID,
          TenantID: o.TenantID,
          CustomerName: o.CustomerName,
          CustomerPhone: o.CustomerPhone,
          GoogleEventID: o.GoogleEventID,
          Status: o.Status,
          ServiceID: o.ServiceID,
          AppUserID: o.AppUserID,
          StartDate: new Date(o.StartDate),
        };
      }
    } catch {
      /* mssql fallback */
    }
  }
  try {
    const db = await getDbPool();
    const result = await db.request().input('id', sql.Int, appointmentId).query(`
        SELECT AppointmentID, TenantID, CustomerName, CustomerPhone, GoogleEventID, Status, ServiceID, AppUserID, StartDate
        FROM Appointments WHERE AppointmentID = @id
      `);
    return (result.recordset[0] as AppointmentDetailRow | undefined) ?? null;
  } catch {
    return null;
  }
}

export async function appointmentExists(appointmentId: number): Promise<boolean> {
  const row = await getAppointmentById(appointmentId);
  return row != null;
}

/** E2E temizliği: sandbox numarasını gri listeden çıkar. */
export async function unblockCustomerPhone(tenantId: number, phoneFragment: string): Promise<void> {
  assertDbWritable();
  const digits = phoneFragment.replace(/\D/g, '').slice(-10);
  const query = `SET NOCOUNT ON; DELETE FROM TenantBlockedPhones WHERE TenantID = ${tenantId} AND PhoneCore LIKE '%${digits}%';`;
  if (useSqlCmd()) {
    runSqlCmdQuery(query);
    return;
  }
  const db = await getDbPool();
  await db
    .request()
    .input('tenantId', sql.Int, tenantId)
    .input('phone', sql.NVarChar, `%${digits}%`)
    .query(`DELETE FROM TenantBlockedPhones WHERE TenantID = @tenantId AND PhoneCore LIKE @phone`);
}

export async function countAppointmentsForPhone(tenantId: number, phoneFragment: string): Promise<number> {
  const digits = phoneFragment.replace(/\D/g, '').slice(-10);
  const q = `SET NOCOUNT ON; SELECT COUNT(*) AS C FROM Appointments WHERE TenantID = ${tenantId} AND CustomerPhone LIKE '%${digits}%'`;
  if (useSqlCmd()) {
    const out = runSqlCmdQuery(q);
    const line = out
      .split(/\r?\n/)
      .map((l) => l.trim())
      .find((l) => /^\d+$/.test(l));
    return line ? Number(line) : 0;
  }
  const db = await getDbPool();
  const result = await db
    .request()
    .input('tenantId', sql.Int, tenantId)
    .input('phone', sql.NVarChar, `%${digits}%`)
    .query(`SELECT COUNT(*) AS C FROM Appointments WHERE TenantID = @tenantId AND CustomerPhone LIKE @phone`);
  return Number(result.recordset[0]?.C ?? 0);
}

function parseSqlCmdJsonRow<T extends Record<string, unknown>>(out: string): T | null {
  const jsonLine = out
    .split(/\r?\n/)
    .map((l) => l.trim())
    .find((l) => l.startsWith('{'));
  if (!jsonLine) return null;
  try {
    return JSON.parse(jsonLine) as T;
  } catch {
    return null;
  }
}

function sqlCmdNVarChar(value: string | null): string {
  if (value == null) return 'NULL';
  return `N'${value.replace(/'/g, "''")}'`;
}

export type TenantBillingSnapshot = {
  IsActive: boolean;
  IsSubscriptionActive: boolean;
  PlanType: string;
  SubscriptionReferenceCode: string | null;
  PendingPlanType: string | null;
  PendingBillingCycle: string | null;
  PendingCheckoutToken: string | null;
  PreviousSubscriptionReferenceCode: string | null;
};

function getTenantSubscriptionStateViaSqlCmd(tenantId: number): {
  IsActive: boolean;
  IsSubscriptionActive: boolean;
  PlanType: string;
  SubscriptionReferenceCode: string | null;
} | null {
  const q = `SET NOCOUNT ON; SELECT IsActive, IsSubscriptionActive, PlanType, SubscriptionReferenceCode FROM Tenants WHERE TenantID = ${tenantId} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;
  const o = parseSqlCmdJsonRow<{
    IsActive?: boolean | number;
    IsSubscriptionActive?: boolean | number;
    PlanType?: string;
    SubscriptionReferenceCode?: string | null;
  }>(runSqlCmdQuery(q));
  if (!o) return null;
  return {
    IsActive: Boolean(o.IsActive),
    IsSubscriptionActive: Boolean(o.IsSubscriptionActive),
    PlanType: String(o.PlanType ?? ''),
    SubscriptionReferenceCode: o.SubscriptionReferenceCode
      ? String(o.SubscriptionReferenceCode)
      : null,
  };
}

function snapshotTenantBillingViaSqlCmd(tenantId: number): TenantBillingSnapshot | null {
  const q = `SET NOCOUNT ON; SELECT IsActive, IsSubscriptionActive, PlanType, SubscriptionReferenceCode, PendingPlanType, PendingBillingCycle, PendingCheckoutToken, PreviousSubscriptionReferenceCode FROM Tenants WHERE TenantID = ${tenantId} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;
  const o = parseSqlCmdJsonRow<{
    IsActive?: boolean | number;
    IsSubscriptionActive?: boolean | number;
    PlanType?: string;
    SubscriptionReferenceCode?: string | null;
    PendingPlanType?: string | null;
    PendingBillingCycle?: string | null;
    PendingCheckoutToken?: string | null;
    PreviousSubscriptionReferenceCode?: string | null;
  }>(runSqlCmdQuery(q));
  if (!o) return null;
  return {
    IsActive: Boolean(o.IsActive),
    IsSubscriptionActive: Boolean(o.IsSubscriptionActive),
    PlanType: String(o.PlanType ?? ''),
    SubscriptionReferenceCode: o.SubscriptionReferenceCode
      ? String(o.SubscriptionReferenceCode)
      : null,
    PendingPlanType: o.PendingPlanType ? String(o.PendingPlanType) : null,
    PendingBillingCycle: o.PendingBillingCycle ? String(o.PendingBillingCycle) : null,
    PendingCheckoutToken: o.PendingCheckoutToken ? String(o.PendingCheckoutToken) : null,
    PreviousSubscriptionReferenceCode: o.PreviousSubscriptionReferenceCode
      ? String(o.PreviousSubscriptionReferenceCode)
      : null,
  };
}

export async function getTenantSubscriptionState(tenantId: number): Promise<{
  IsActive: boolean;
  IsSubscriptionActive: boolean;
  PlanType: string;
  SubscriptionReferenceCode: string | null;
}> {
  if (useSqlCmd()) {
    try {
      const row = getTenantSubscriptionStateViaSqlCmd(tenantId);
      if (row) return row;
    } catch {
      /* mssql */
    }
  }
  const db = await getDbPool();
  const result = await db.request().input('tenantId', sql.Int, tenantId).query(`
      SELECT IsActive, IsSubscriptionActive, PlanType, SubscriptionReferenceCode
      FROM Tenants WHERE TenantID = @tenantId
    `);
  const row = result.recordset[0];
  if (!row) throw new Error(`Tenant ${tenantId} not found`);
  return row;
}

export async function isTenantSuspendedInDb(tenantId: number): Promise<boolean> {
  if (tenantId <= 0) return false;
  try {
    const state = await getTenantSubscriptionState(tenantId);
    return !state.IsActive || !state.IsSubscriptionActive;
  } catch {
    return false;
  }
}

export async function snapshotTenantBilling(tenantId: number): Promise<TenantBillingSnapshot> {
  if (useSqlCmd()) {
    try {
      const row = snapshotTenantBillingViaSqlCmd(tenantId);
      if (row) return row;
    } catch {
      /* mssql */
    }
  }
  const db = await getDbPool();
  const result = await db.request().input('tenantId', sql.Int, tenantId).query(`
      SELECT IsActive, IsSubscriptionActive, PlanType,
             SubscriptionReferenceCode, PendingPlanType, PendingBillingCycle,
             PendingCheckoutToken, PreviousSubscriptionReferenceCode
      FROM Tenants WHERE TenantID = @tenantId
    `);
  const row = result.recordset[0];
  if (!row) throw new Error(`Tenant ${tenantId} not found`);
  return {
    IsActive: Boolean(row.IsActive),
    IsSubscriptionActive: Boolean(row.IsSubscriptionActive),
    PlanType: String(row.PlanType ?? ''),
    SubscriptionReferenceCode: row.SubscriptionReferenceCode
      ? String(row.SubscriptionReferenceCode)
      : null,
    PendingPlanType: row.PendingPlanType ? String(row.PendingPlanType) : null,
    PendingBillingCycle: row.PendingBillingCycle ? String(row.PendingBillingCycle) : null,
    PendingCheckoutToken: row.PendingCheckoutToken ? String(row.PendingCheckoutToken) : null,
    PreviousSubscriptionReferenceCode: row.PreviousSubscriptionReferenceCode
      ? String(row.PreviousSubscriptionReferenceCode)
      : null,
  };
}

function unlockTenantUsersViaSqlCmd(tenantId: number): void {
  runSqlCmdQuery(
    `SET NOCOUNT ON; UPDATE AppUsers SET LockoutEnd = NULL, AccessFailedCount = 0 WHERE TenantID = ${tenantId}`,
  );
}

function restoreTenantBillingViaSqlCmd(tenantId: number, snap: TenantBillingSnapshot): void {
  const q = `SET NOCOUNT ON; UPDATE Tenants SET IsActive = ${snap.IsActive ? 1 : 0}, IsSubscriptionActive = ${snap.IsSubscriptionActive ? 1 : 0}, PlanType = ${sqlCmdNVarChar(snap.PlanType)}, SubscriptionReferenceCode = ${sqlCmdNVarChar(snap.SubscriptionReferenceCode)}, PendingPlanType = ${sqlCmdNVarChar(snap.PendingPlanType)}, PendingBillingCycle = ${sqlCmdNVarChar(snap.PendingBillingCycle)}, PendingCheckoutToken = ${sqlCmdNVarChar(snap.PendingCheckoutToken)}, PreviousSubscriptionReferenceCode = ${sqlCmdNVarChar(snap.PreviousSubscriptionReferenceCode)} WHERE TenantID = ${tenantId}`;
  runSqlCmdQuery(q);
  if (snap.IsActive && snap.IsSubscriptionActive) unlockTenantUsersViaSqlCmd(tenantId);
}

export async function restoreTenantBilling(
  tenantId: number,
  snap: TenantBillingSnapshot,
): Promise<void> {
  if (useSqlCmd()) {
    try {
      restoreTenantBillingViaSqlCmd(tenantId, snap);
      return;
    } catch {
      /* mssql */
    }
  }
  const db = await getDbPool();
  await db
    .request()
    .input('tenantId', sql.Int, tenantId)
    .input('isActive', sql.Bit, snap.IsActive)
    .input('isSubActive', sql.Bit, snap.IsSubscriptionActive)
    .input('planType', sql.NVarChar, snap.PlanType)
    .input('subRef', sql.NVarChar, snap.SubscriptionReferenceCode)
    .input('pendingPlan', sql.NVarChar, snap.PendingPlanType)
    .input('pendingCycle', sql.NVarChar, snap.PendingBillingCycle)
    .input('pendingToken', sql.NVarChar, snap.PendingCheckoutToken)
    .input('prevRef', sql.NVarChar, snap.PreviousSubscriptionReferenceCode)
    .query(`
      UPDATE Tenants SET
        IsActive = @isActive,
        IsSubscriptionActive = @isSubActive,
        PlanType = @planType,
        SubscriptionReferenceCode = @subRef,
        PendingPlanType = @pendingPlan,
        PendingBillingCycle = @pendingCycle,
        PendingCheckoutToken = @pendingToken,
        PreviousSubscriptionReferenceCode = @prevRef
      WHERE TenantID = @tenantId
    `);
  if (snap.IsActive && snap.IsSubscriptionActive) {
    await db.request().input('tenantId', sql.Int, tenantId).query(`
        UPDATE AppUsers SET LockoutEnd = NULL, AccessFailedCount = 0 WHERE TenantID = @tenantId
      `);
  }
}

function setTenantPendingPlanChangeViaSqlCmd(
  tenantId: number,
  opts: {
    pendingPlanType: string;
    pendingBillingCycle: string;
    pendingCheckoutToken: string;
    previousSubscriptionReferenceCode: string;
    subscriptionReferenceCode: string;
  },
): void {
  const q = `SET NOCOUNT ON; UPDATE Tenants SET IsActive = 1, IsSubscriptionActive = 1, PendingPlanType = ${sqlCmdNVarChar(opts.pendingPlanType)}, PendingBillingCycle = ${sqlCmdNVarChar(opts.pendingBillingCycle)}, PendingCheckoutToken = ${sqlCmdNVarChar(opts.pendingCheckoutToken)}, PreviousSubscriptionReferenceCode = ${sqlCmdNVarChar(opts.previousSubscriptionReferenceCode)}, SubscriptionReferenceCode = ${sqlCmdNVarChar(opts.subscriptionReferenceCode)} WHERE TenantID = ${tenantId}`;
  runSqlCmdQuery(q);
}

/** Plan değişikliği ödemesi yarım kaldı senaryosu (webhook failure testi). */
export async function setTenantPendingPlanChange(
  tenantId: number,
  opts: {
    pendingPlanType: string;
    pendingBillingCycle: string;
    pendingCheckoutToken: string;
    previousSubscriptionReferenceCode: string;
    subscriptionReferenceCode: string;
  },
): Promise<void> {
  if (useSqlCmd()) {
    try {
      setTenantPendingPlanChangeViaSqlCmd(tenantId, opts);
      return;
    } catch {
      /* mssql */
    }
  }
  const db = await getDbPool();
  await db
    .request()
    .input('tenantId', sql.Int, tenantId)
    .input('pendingPlan', sql.NVarChar, opts.pendingPlanType)
    .input('pendingCycle', sql.NVarChar, opts.pendingBillingCycle)
    .input('pendingToken', sql.NVarChar, opts.pendingCheckoutToken)
    .input('prevRef', sql.NVarChar, opts.previousSubscriptionReferenceCode)
    .input('subRef', sql.NVarChar, opts.subscriptionReferenceCode)
    .query(`
      UPDATE Tenants SET
        IsActive = 1,
        IsSubscriptionActive = 1,
        PendingPlanType = @pendingPlan,
        PendingBillingCycle = @pendingCycle,
        PendingCheckoutToken = @pendingToken,
        PreviousSubscriptionReferenceCode = @prevRef,
        SubscriptionReferenceCode = @subRef
      WHERE TenantID = @tenantId
    `);
}

/** Webhook askısı / lockout sonrası panel OTP için tenant + kullanıcıları aç. */
export async function ensureE2eTenantBillingActive(tenantId: number): Promise<void> {
  if (tenantId <= 0) return;

  const billingSql = `SET NOCOUNT ON; UPDATE Tenants SET IsActive = 1, IsSubscriptionActive = 1 WHERE TenantID = ${tenantId}; UPDATE AppUsers SET LockoutEnd = NULL, AccessFailedCount = 0 WHERE TenantID = ${tenantId}`;

  if (useSqlCmd()) {
    try {
      runSqlCmdQuery(billingSql);
      return;
    } catch (sqlCmdErr) {
      if (process.env.E2E_ALLOW_MSSQL_FALLBACK !== 'true') {
        throw new Error(
          `sqlcmd ile tenant ${tenantId} aktifleştirilemedi: ${(sqlCmdErr as Error).message}`,
        );
      }
    }
  }

  const db = await getDbPool();
  await db.request().input('tenantId', sql.Int, tenantId).query(`
      UPDATE Tenants SET IsActive = 1, IsSubscriptionActive = 1 WHERE TenantID = @tenantId;
      UPDATE AppUsers SET LockoutEnd = NULL, AccessFailedCount = 0 WHERE TenantID = @tenantId;
    `);
}

export type OtpRow = {
  OtpCode: string;
  OtpExpiry: Date | null;
  LastOtpRequestDate: Date | null;
};

export async function getOtpStateForPhone(phoneNumber: string): Promise<OtpRow | null> {
  const digits = phoneNumber.replace(/\D/g, '').slice(-10);
  const jsonQuery = `SET NOCOUNT ON; SELECT TOP 1 OtpCode, CONVERT(varchar(30), OtpExpiry, 126) AS OtpExpiry, CONVERT(varchar(30), LastOtpRequestDate, 126) AS LastOtpRequestDate FROM AppUsers WHERE PhoneNumber LIKE '%${digits}%' AND OtpCode IS NOT NULL ORDER BY AppUserID DESC FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;
  if (useSqlCmd()) {
    try {
      const out = runSqlCmdQuery(jsonQuery);
      const jsonLine = out
        .split(/\r?\n/)
        .map((l) => l.trim())
        .find((l) => l.startsWith('{'));
      if (jsonLine) {
        const o = JSON.parse(jsonLine) as {
          OtpCode: string;
          OtpExpiry?: string | null;
          LastOtpRequestDate?: string | null;
        };
        return {
          OtpCode: String(o.OtpCode),
          OtpExpiry: o.OtpExpiry ? new Date(o.OtpExpiry) : null,
          LastOtpRequestDate: o.LastOtpRequestDate ? new Date(o.LastOtpRequestDate) : null,
        };
      }
    } catch {
      /* mssql */
    }
  }
  try {
    const db = await getDbPool();
    const result = await db
      .request()
      .input('phone', sql.NVarChar, `%${digits}%`)
      .query(`
        SELECT TOP 1 OtpCode, OtpExpiry, LastOtpRequestDate
        FROM AppUsers WHERE PhoneNumber LIKE @phone AND OtpCode IS NOT NULL
        ORDER BY AppUserID DESC
      `);
    const row = result.recordset[0];
    if (!row?.OtpCode) return null;
    return {
      OtpCode: String(row.OtpCode),
      OtpExpiry: row.OtpExpiry ? new Date(row.OtpExpiry) : null,
      LastOtpRequestDate: row.LastOtpRequestDate ? new Date(row.LastOtpRequestDate) : null,
    };
  } catch {
    return null;
  }
}

/** generate-otp sonrası geçerli ve mümkünse yeni kodu bekle (eski OTP ile doğrulama hatasını önler). */
export async function waitForFreshOtpInDatabase(
  phoneNumber: string,
  options?: { requestedAfterMs?: number; timeoutMs?: number },
): Promise<string> {
  const timeoutMs = options?.timeoutMs ?? Number(process.env.E2E_OTP_POLL_TIMEOUT_MS ?? 60_000);
  const requestedAfterMs = options?.requestedAfterMs ?? Date.now() - 2000;
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const row = await getOtpStateForPhone(phoneNumber);
    if (row?.OtpCode && row.OtpExpiry && row.OtpExpiry.getTime() > Date.now()) {
      const reqAt = row.LastOtpRequestDate?.getTime() ?? 0;
      if (reqAt >= requestedAfterMs - 5000) {
        return row.OtpCode.replace(/\D/g, '').padStart(6, '0').slice(-6);
      }
    }
    await new Promise((r) => setTimeout(r, 1500));
  }
  throw new Error(
    `OTP DB'de ${timeoutMs}ms içinde taze kod görünmedi (${phoneNumber}). Cooldown (45sn) veya Evolution gecikmesi olabilir.`,
  );
}

export async function getOtpForPhone(phoneNumber: string): Promise<string | null> {
  if (useSqlCmd()) {
    try {
      return getOtpForPhoneViaSqlCmd(phoneNumber);
    } catch (e) {
      if (!hasEnv('E2E_DATABASE_URL') && !hasEnv('E2E_DB_SERVER')) throw e;
    }
  }

  const db = await getDbPool();
  const digits = phoneNumber.replace(/\D/g, '');
  const result = await db.request().input('phone', sql.NVarChar, `%${digits.slice(-10)}%`).query(`
      SELECT TOP 1 OtpCode, OtpExpiry FROM AppUsers WHERE PhoneNumber LIKE @phone ORDER BY AppUserID DESC
    `);
  const row = result.recordset[0];
  if (!row?.OtpCode) return null;
  if (row.OtpExpiry && new Date(row.OtpExpiry) < new Date()) return null;
  return String(row.OtpCode);
}

/** WhatsApp gecikmesi: DB'de OtpCode dolana kadar poll (varsayılan ~25 sn). */
export async function waitForOtpInDatabase(
  phoneNumber: string,
  timeoutMs = Number(process.env.E2E_OTP_POLL_TIMEOUT_MS ?? 45_000),
  intervalMs = 3_000,
): Promise<string> {
  const deadline = Date.now() + timeoutMs;
  let last: string | null = null;
  while (Date.now() < deadline) {
    last = await getOtpForPhone(phoneNumber);
    if (last) return last;
    await new Promise((r) => setTimeout(r, intervalMs));
  }
  throw new Error(
    `OTP DB'de ${timeoutMs}ms içinde görünmedi (${phoneNumber}). Evolution gecikmesi veya E2E_DATABASE_URL kontrol edin.`,
  );
}

export async function staffHasGoogleRefreshToken(appUserId: number): Promise<boolean> {
  const db = await getDbPool();
  const result = await db.request().input('id', sql.Int, appUserId).query(`
      SELECT CASE WHEN GoogleRefreshToken IS NOT NULL AND LEN(GoogleRefreshToken) > 0 THEN 1 ELSE 0 END AS HasToken
      FROM AppUsers WHERE AppUserID = @id
    `);
  return result.recordset[0]?.HasToken === 1;
}

export type TenantE2eConfig = {
  tenantId: number;
  apiKey: string | null;
  instanceName: string | null;
};

/** E2E tenant: ApiKey + InstanceName (n8n contract). */
export async function getTenantE2eConfig(tenantId: number): Promise<TenantE2eConfig | null> {
  if (tenantId <= 0) return null;
  const jsonQuery = `SET NOCOUNT ON; SELECT ApiKey, InstanceName FROM Tenants WHERE TenantID = ${tenantId} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;
  if (useSqlCmd()) {
    try {
      const out = runSqlCmdQuery(jsonQuery);
      const jsonLine = out
        .split(/\r?\n/)
        .map((l) => l.trim())
        .find((l) => l.startsWith('{'));
      if (jsonLine) {
        const o = JSON.parse(jsonLine) as { ApiKey?: string; InstanceName?: string };
        const apiKey = String(o.ApiKey ?? '').trim();
        const instanceName = String(o.InstanceName ?? '').trim();
        return {
          tenantId,
          apiKey: apiKey.length > 0 ? apiKey : null,
          instanceName: instanceName.length > 0 ? instanceName : null,
        };
      }
    } catch {
      /* mssql */
    }
  }
  try {
    const db = await getDbPool();
    const result = await db.request().input('tenantId', sql.Int, tenantId).query(`
        SELECT ApiKey, InstanceName FROM Tenants WHERE TenantID = @tenantId
      `);
    const row = result.recordset[0];
    if (!row) return null;
    const apiKey = String(row.ApiKey ?? '').trim();
    const instanceName = String(row.InstanceName ?? '').trim();
    return {
      tenantId,
      apiKey: apiKey.length > 0 ? apiKey : null,
      instanceName: instanceName.length > 0 ? instanceName : null,
    };
  } catch {
    return null;
  }
}

/** n8n X-Auth-Token = Tenants.ApiKey (E2E_TENANT_ID ile). */
export async function getTenantApiKey(tenantId: number): Promise<string | null> {
  if (tenantId <= 0) return null;
  const q = `SET NOCOUNT ON; SELECT ApiKey FROM Tenants WHERE TenantID = ${tenantId}`;
  if (useSqlCmd()) {
    try {
      const out = runSqlCmdQuery(q);
      const line = out
        .split(/\r?\n/)
        .map((l) => l.trim())
        .find((l) => l.length >= 16 && !l.startsWith('('));
      if (line) return line;
    } catch {
      /* mssql */
    }
  }
  try {
    const db = await getDbPool();
    const result = await db.request().input('tenantId', sql.Int, tenantId).query(`
        SELECT ApiKey FROM Tenants WHERE TenantID = @tenantId
      `);
    const key = String(result.recordset[0]?.ApiKey ?? '').trim();
    return key.length > 0 ? key : null;
  } catch {
    return null;
  }
}

export function dbConfigured(): boolean {
  return (
    hasEnv('E2E_DB_SERVER') ||
    hasEnv('E2E_DATABASE_URL') ||
    hasEnv('ConnectionStrings__DefaultConnection')
  );
}

export function requireDbConfigured(): void {
  if (!dbConfigured()) requireEnv('E2E_DATABASE_URL');
}

export type TenantBreakTimeState = {
  enabled: boolean;
  start: string;
  end: string;
};

function normalizeHm(value: unknown): string {
  const s = String(value ?? '').trim();
  const m = s.match(/^(\d{1,2}):(\d{2})/);
  if (!m) return s;
  return `${m[1].padStart(2, '0')}:${m[2]}`;
}

function parseBreakTimeRow(line: string): TenantBreakTimeState | null {
  const parts = line.trim().split(/\s+/);
  if (parts.length < 3) return null;
  const enabled = parts[0] === '1' || parts[0].toLowerCase() === 'true';
  return { enabled, start: normalizeHm(parts[1]), end: normalizeHm(parts[2]) };
}

export async function getTenantBreakTime(tenantId: number): Promise<TenantBreakTimeState> {
  const q = `SET NOCOUNT ON; SELECT BreakTimeEnabled, CONVERT(varchar(5), BreakStartTime, 108) AS S, CONVERT(varchar(5), BreakEndTime, 108) AS E FROM Tenants WHERE TenantID = ${tenantId}`;
  if (useSqlCmd()) {
    const out = runSqlCmdQuery(q);
    const line = out
      .split(/\r?\n/)
      .map((l) => l.trim())
      .find((l) => /^[01]\s+\d{1,2}:\d{2}/.test(l));
    const parsed = line ? parseBreakTimeRow(line) : null;
    if (parsed) return parsed;
  }
  const db = await getDbPool();
  const result = await db.request().input('tenantId', sql.Int, tenantId).query(`
      SELECT BreakTimeEnabled, BreakStartTime, BreakEndTime FROM Tenants WHERE TenantID = @tenantId
    `);
  const row = result.recordset[0];
  if (!row) throw new Error(`Tenant ${tenantId} not found`);
  return {
    enabled: Boolean(row.BreakTimeEnabled),
    start: normalizeHm(row.BreakStartTime),
    end: normalizeHm(row.BreakEndTime),
  };
}

/** E2E tenant: mola açık, varsayılan 12:00–13:00. */
export async function ensureE2eBreakTime(
  tenantId: number,
  start = '12:00',
  end = '13:00',
  enabled = true,
): Promise<void> {
  assertDbWritable();
  const en = enabled ? 1 : 0;
  const query = `SET NOCOUNT ON; UPDATE Tenants SET BreakTimeEnabled = ${en}, BreakStartTime = '${start}:00', BreakEndTime = '${end}:00' WHERE TenantID = ${tenantId};`;
  if (useSqlCmd()) {
    runSqlCmdQuery(query);
    return;
  }
  const db = await getDbPool();
  await db.request().query(query);
}

/**
 * E2E tenant için haftalık çalışma saatleri (Pazar kapalı, diğer günler 08:00–21:00).
 */
export async function ensureE2eBusinessHours(tenantId: number): Promise<void> {
  assertDbWritable();
  const open = '08:00:00';
  const close = '21:00:00';
  const deletes = `DELETE FROM BusinessHours WHERE TenantID = ${tenantId};`;
  const inserts: string[] = [];
  for (let day = 0; day < 7; day++) {
    const closed = day === 0 ? 1 : 0;
    const op = day === 0 ? '00:00:00' : open;
    const cl = day === 0 ? '00:00:00' : close;
    inserts.push(
      `INSERT INTO BusinessHours (TenantID, DayOfWeek, OpenTime, CloseTime, IsClosed) VALUES (${tenantId}, ${day}, '${op}', '${cl}', ${closed});`,
    );
  }
  const query = `SET NOCOUNT ON; ${deletes} ${inserts.join(' ')}`;
  if (useSqlCmd()) {
    try {
      runSqlCmdQuery(query);
      return;
    } catch (sqlCmdErr) {
      if (process.env.E2E_ALLOW_MSSQL_FALLBACK !== 'true') {
        throw new Error(
          `sqlcmd ile tenant ${tenantId} çalışma saatleri ayarlanamadı: ${(sqlCmdErr as Error).message}`,
        );
      }
      if (!isSqlCmdTimeoutError(sqlCmdErr)) throw sqlCmdErr;
    }
  }
  const db = await getDbPool();
  await db.request().query(query);
}

export type SecurityBootstrapRow = {
  tenantAId: number;
  tenantAToken: string;
  tenantBId: number;
  tenantBToken: string;
  tenantBAppointmentId: number;
  passiveTenantId: number;
  passiveTenantToken: string;
  passiveInstanceName: string;
  passiveManagerPhone: string;
};

export type SecurityTenantCandidate = {
  tenantId: number;
  apiKey: string;
  instanceName: string;
  serviceId: number;
  staffId: number;
};

export async function getLatestAppointmentIdForTenant(tenantId: number): Promise<number | null> {
  const q = `SET NOCOUNT ON; SELECT TOP 1 AppointmentID FROM Appointments WHERE TenantID = ${tenantId} ORDER BY AppointmentID DESC`;
  if (useSqlCmd()) {
    try {
      const line = runSqlCmdQuery(q)
        .split(/\r?\n/)
        .map((l) => l.trim())
        .find((l) => /^\d+$/.test(l));
      if (line) return Number(line);
    } catch {
      /* mssql */
    }
  }
  try {
    const db = await getDbPool();
    const r = await db.request().input('tid', sql.Int, tenantId).query(
      `SELECT TOP 1 AppointmentID FROM Appointments WHERE TenantID = @tid ORDER BY AppointmentID DESC`,
    );
    const id = r.recordset[0]?.AppointmentID;
    return id != null ? Number(id) : null;
  } catch {
    return null;
  }
}

/** Tenant A dışında ApiKey + instance olan herhangi bir kiracı (aktif abonelik şart değil). */
export async function findSecurityTenantCandidate(
  excludeTenantId: number,
): Promise<SecurityTenantCandidate | null> {
  const ex = excludeTenantId;
  const q = `
SET NOCOUNT ON;
SELECT TOP 1
  t.TenantID AS tenantId,
  t.ApiKey AS apiKey,
  t.InstanceName AS instanceName,
  (SELECT TOP 1 s.ServiceID FROM Services s WHERE s.TenantID = t.TenantID ORDER BY s.ServiceID) AS serviceId,
  (SELECT TOP 1 u.AppUserID FROM AppUsers u
   INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId
   INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id
   WHERE u.TenantID = t.TenantID AND oc.Name = 'Manager' ORDER BY u.AppUserID) AS staffId
FROM Tenants t
WHERE t.TenantID <> ${ex}
  AND LEN(ISNULL(t.ApiKey,'')) > 8
  AND t.InstanceName IS NOT NULL
ORDER BY t.TenantID DESC
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;

  if (useSqlCmd()) {
    try {
      const o = parseSqlCmdJsonRow<{
        tenantId?: number;
        apiKey?: string;
        instanceName?: string;
        serviceId?: number;
        staffId?: number;
      }>(runSqlCmdQuery(q));
      if (o?.tenantId && o.serviceId && o.staffId && o.apiKey && o.instanceName) {
        return {
          tenantId: Number(o.tenantId),
          apiKey: String(o.apiKey).trim(),
          instanceName: String(o.instanceName).trim(),
          serviceId: Number(o.serviceId),
          staffId: Number(o.staffId),
        };
      }
    } catch {
      /* mssql */
    }
  }

  try {
    const db = await getDbPool();
    const r = await db.request().input('ex', sql.Int, excludeTenantId).query(`
      SELECT TOP 1
        t.TenantID AS tenantId,
        t.ApiKey AS apiKey,
        t.InstanceName AS instanceName,
        (SELECT TOP 1 s.ServiceID FROM Services s WHERE s.TenantID = t.TenantID ORDER BY s.ServiceID) AS serviceId,
        (SELECT TOP 1 u.AppUserID FROM AppUsers u
         INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId
         INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id
         WHERE u.TenantID = t.TenantID AND oc.Name = 'Manager' ORDER BY u.AppUserID) AS staffId
      FROM Tenants t
      WHERE t.TenantID <> @ex
        AND LEN(ISNULL(t.ApiKey,'')) > 8
        AND t.InstanceName IS NOT NULL
      ORDER BY t.TenantID DESC
    `);
    const row = r.recordset[0] as SecurityTenantCandidate | undefined;
    if (!row?.tenantId || !row.serviceId || !row.staffId) return null;
    return {
      tenantId: Number(row.tenantId),
      apiKey: String(row.apiKey).trim(),
      instanceName: String(row.instanceName).trim(),
      serviceId: Number(row.serviceId),
      staffId: Number(row.staffId),
    };
  } catch {
    return null;
  }
}

export async function getManagerPhoneForTenant(tenantId: number): Promise<string | null> {
  const q = `SET NOCOUNT ON; SELECT TOP 1 u.PhoneNumber AS phone FROM AppUsers u INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id WHERE u.TenantID = ${tenantId} AND oc.Name = 'Manager' AND u.Status = 1 AND LEN(ISNULL(u.PhoneNumber,'')) >= 10 ORDER BY u.AppUserID DESC FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;

  if (useSqlCmd()) {
    try {
      const o = parseSqlCmdJsonRow<{ phone?: string }>(runSqlCmdQuery(q));
      if (o?.phone) {
        const d = String(o.phone).replace(/\D/g, '');
        if (d.length >= 10) return d.length === 10 ? `0${d}` : `0${d.slice(-10)}`;
      }
    } catch {
      /* mssql */
    }
  }

  try {
    const db = await getDbPool();
    const r = await db.request().input('tid', sql.Int, tenantId).query(`
      SELECT TOP 1 u.PhoneNumber
      FROM AppUsers u
      INNER JOIN UserOperationClaims uoc ON u.AppUserID = uoc.UserId
      INNER JOIN OperationClaims oc ON uoc.OperationClaimId = oc.Id
      WHERE u.TenantID = @tid AND oc.Name = 'Manager' AND u.Status = 1 AND LEN(ISNULL(u.PhoneNumber,'')) >= 10
      ORDER BY u.AppUserID DESC
    `);
    const phone = String(r.recordset[0]?.PhoneNumber ?? '').trim();
    return phone || null;
  } catch {
    return null;
  }
}

export async function findPassiveTenantInDb(): Promise<{
  tenantId: number;
  apiKey: string;
  instanceName: string;
  managerPhone: string;
} | null> {
  const q = `
SET NOCOUNT ON;
SELECT TOP 1 t.TenantID AS tenantId, t.ApiKey AS apiKey, t.InstanceName AS instanceName
FROM Tenants t
WHERE (t.IsSubscriptionActive = 0 OR t.IsActive = 0) AND t.InstanceName IS NOT NULL AND LEN(ISNULL(t.ApiKey,'')) > 8
ORDER BY t.TenantID DESC
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;

  let tenantId = 0;
  let apiKey = '';
  let instanceName = '';

  if (useSqlCmd()) {
    try {
      const o = parseSqlCmdJsonRow<{
        tenantId?: number;
        apiKey?: string;
        instanceName?: string;
      }>(runSqlCmdQuery(q));
      if (o?.tenantId && o.apiKey && o.instanceName) {
        tenantId = Number(o.tenantId);
        apiKey = String(o.apiKey).trim();
        instanceName = String(o.instanceName).trim();
      }
    } catch {
      /* mssql */
    }
  }

  if (!tenantId) {
    try {
      const db = await getDbPool();
      const r = await db.request().query(`
        SELECT TOP 1 TenantID AS tenantId, ApiKey AS apiKey, InstanceName AS instanceName
        FROM Tenants
        WHERE (IsSubscriptionActive = 0 OR IsActive = 0) AND InstanceName IS NOT NULL AND LEN(ISNULL(ApiKey,'')) > 8
        ORDER BY TenantID DESC
      `);
      const row = r.recordset[0];
      if (!row) return null;
      tenantId = Number(row.tenantId);
      apiKey = String(row.apiKey).trim();
      instanceName = String(row.instanceName).trim();
    } catch {
      return null;
    }
  }

  if (!tenantId || !apiKey || !instanceName) return null;
  const managerPhone = await getManagerPhoneForTenant(tenantId);
  if (!managerPhone) return null;
  return { tenantId, apiKey, instanceName, managerPhone };
}

export type SecurityProvisionResult = {
  row: SecurityBootstrapRow;
  restore: () => Promise<void>;
};

/**
 * Güvenlik E2E: Tenant B + randevu + pasif tenant (yoksa B geçici askıya alınır).
 */
export async function provisionSecurityTestEnv(
  tenantAId: number,
  tenantAToken: string,
): Promise<SecurityProvisionResult | null> {
  if (tenantAId <= 0 || !tenantAToken.trim()) return null;

  const tenantB = await findSecurityTenantCandidate(tenantAId);
  if (!tenantB) return null;

  let appointmentId = await getLatestAppointmentIdForTenant(tenantB.tenantId);
  const restores: Array<() => Promise<void>> = [];

  if (!appointmentId) {
    const { postN8nAppointment } = await import('./webhooks');
    await ensureE2eBusinessHours(tenantB.tenantId);
    await ensureE2eBreakTime(tenantB.tenantId);
    const d = new Date();
    d.setDate(d.getDate() + 21);
    d.setHours(11, 0, 0, 0);
    const pad = (n: number) => String(n).padStart(2, '0');
    const iso = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
    const phone = `5320000${String(tenantB.tenantId).padStart(3, '0')}`.slice(0, 11);
    const res = await postN8nAppointment(
      {
        customerName: 'Esse Security Test',
        customerPhone: phone,
        businessPhone: tenantB.instanceName,
        serviceID: tenantB.serviceId,
        appUserID: tenantB.staffId,
        startDate: iso,
      },
      tenantB.apiKey,
      tenantB.tenantId,
    );
    if (res.status === 200) {
      const body = res.json as { ID?: number; id?: number };
      appointmentId = Number(body.ID ?? body.id ?? 0) || (await getLatestAppointmentIdForTenant(tenantB.tenantId));
    }
    if (appointmentId) {
      restores.push(async () => {
        const { deleteAppointmentAsN8n } = await import('./appointments');
        await deleteAppointmentAsN8n(appointmentId!, tenantB.apiKey, tenantB.tenantId).catch(() => {});
      });
    }
  }

  if (!appointmentId) return null;

  let passiveTenantId = 0;
  let passiveToken = '';
  let passiveInstance = '';
  let passivePhone = '';

  const passive = await findPassiveTenantInDb();
  if (passive && (await isTenantSuspendedInDb(passive.tenantId))) {
    passiveTenantId = passive.tenantId;
    passiveToken = passive.apiKey;
    passiveInstance = passive.instanceName;
    passivePhone = passive.managerPhone;
  } else {
    const snap = await snapshotTenantBilling(tenantB.tenantId);
    await setTenantSubscriptionSuspended(tenantB.tenantId, true);
    passiveTenantId = tenantB.tenantId;
    passiveToken = tenantB.apiKey;
    passiveInstance = tenantB.instanceName;
    passivePhone = (await getManagerPhoneForTenant(tenantB.tenantId)) ?? '';
    restores.push(async () => {
      await restoreTenantBilling(tenantB.tenantId, snap);
      await ensureE2eTenantBillingActive(tenantB.tenantId).catch(() => {});
    });
  }

  if (!passivePhone) return null;

  return {
    row: {
      tenantAId,
      tenantAToken: tenantAToken.trim(),
      tenantBId: tenantB.tenantId,
      tenantBToken: tenantB.apiKey,
      tenantBAppointmentId: appointmentId,
      passiveTenantId,
      passiveTenantToken: passiveToken,
      passiveInstanceName: passiveInstance,
      passiveManagerPhone: passivePhone,
    },
    restore: async () => {
      for (const fn of restores.reverse()) {
        await fn();
      }
    },
  };
}

/** discover-env.ps1 ile aynı mantık — security.spec beforeAll için. */
export async function fetchSecurityBootstrapFromDb(): Promise<SecurityBootstrapRow | null> {
  const tenantAId = Number(process.env.E2E_TENANT_ID ?? process.env.E2E_TENANT_A_ID ?? 0);
  const tenantAToken =
    process.env.E2E_N8N_TOKEN?.trim() ?? process.env.E2E_TENANT_A_TOKEN?.trim() ?? '';
  const provisioned = await provisionSecurityTestEnv(tenantAId, tenantAToken);
  return provisioned?.row ?? null;
}

function findTenantIdByEmailSqlCmd(email: string): number | null {
  const safe = email.replace(/'/g, "''");
  const q = `SET NOCOUNT ON; SELECT TOP 1 TenantID FROM AppUsers WHERE LOWER(Email) = LOWER(N'${safe}') ORDER BY AppUserID DESC`;
  const out = runSqlCmdQuery(q);
  const line = out
    .split(/\r?\n/)
    .map((l) => l.trim())
    .find((l) => /^\d+$/.test(l));
  return line ? Number(line) : null;
}

/** Kayıt testi: e-posta ile tenant bulma. */
export async function findTenantIdByEmail(email: string): Promise<number | null> {
  const safe = email.replace(/'/g, "''");
  if (useSqlCmd()) {
    try {
      const id = findTenantIdByEmailSqlCmd(email);
      if (id) return id;
      const q = `SET NOCOUNT ON; SELECT TOP 1 TenantID FROM AppUsers WHERE LOWER(Email) = LOWER(N'${safe}') ORDER BY AppUserID DESC FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;
      const o = parseSqlCmdJsonRow<{ TenantID?: number }>(runSqlCmdQuery(q));
      if (o?.TenantID) return Number(o.TenantID);
    } catch {
      /* mssql */
    }
  }
  const db = await getDbPool();
  const result = await db
    .request()
    .input('email', sql.NVarChar, email.trim().toLowerCase())
    .query(`SELECT TOP 1 TenantID FROM AppUsers WHERE LOWER(Email) = @email ORDER BY AppUserID DESC`);
  const row = result.recordset[0];
  return row?.TenantID != null ? Number(row.TenantID) : null;
}

/** Kayıt testi: telefon ile tenant bulma (e-posta gecikirse yedek). */
export async function findTenantIdByPhone(phone: string): Promise<number | null> {
  const digits = phone.replace(/\D/g, '').slice(-10);
  if (digits.length < 10) return null;
  const q = `SET NOCOUNT ON; SELECT TOP 1 TenantID FROM AppUsers WHERE PhoneNumber LIKE '%${digits}%' ORDER BY AppUserID DESC`;
  if (useSqlCmd()) {
    try {
      const out = runSqlCmdQuery(q);
      const line = out
        .split(/\r?\n/)
        .map((l) => l.trim())
        .find((l) => /^\d+$/.test(l));
      if (line) return Number(line);
    } catch {
      /* mssql */
    }
  }
  const db = await getDbPool();
  const result = await db
    .request()
    .input('digits', sql.NVarChar, `%${digits}%`)
    .query(`SELECT TOP 1 TenantID FROM AppUsers WHERE PhoneNumber LIKE @digits ORDER BY AppUserID DESC`);
  const row = result.recordset[0];
  return row?.TenantID != null ? Number(row.TenantID) : null;
}

export async function getTenantPendingCheckoutToken(tenantId: number): Promise<string | null> {
  const snap = await snapshotTenantBilling(tenantId);
  return snap.PendingCheckoutToken;
}

/** Pasif tenant senaryoları için abonelik askısı (geri alınabilir). */
export async function setTenantSubscriptionSuspended(
  tenantId: number,
  suspended: boolean,
): Promise<void> {
  assertDbWritable();
  const active = suspended ? 0 : 1;
  const q = `SET NOCOUNT ON; UPDATE Tenants SET IsActive = ${active}, IsSubscriptionActive = ${active} WHERE TenantID = ${tenantId}`;
  if (useSqlCmd()) {
    runSqlCmdQuery(q);
    return;
  }
  const db = await getDbPool();
  await db
    .request()
    .input('tenantId', sql.Int, tenantId)
    .input('active', sql.Bit, !suspended)
    .query(
      `UPDATE Tenants SET IsActive = @active, IsSubscriptionActive = @active WHERE TenantID = @tenantId`,
    );
}

/**
 * E2E kayıt testi temizliği — yalnızca e2e-reg-* e-postalı tenant'lar silinmeli.
 */
export async function deleteE2eTenantCascade(tenantId: number, expectedEmailPrefix = 'e2e-reg-'): Promise<void> {
  assertDbWritable();
  if (tenantId <= 0) return;

  const prefixEsc = expectedEmailPrefix.replace(/'/g, "''");
  const verifyQ = `SET NOCOUNT ON; SELECT TOP 1 Email FROM AppUsers WHERE TenantID = ${tenantId} AND Email LIKE N'${prefixEsc}%' FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;
  let ownerEmail = '';
  if (useSqlCmd()) {
    const o = parseSqlCmdJsonRow<{ Email?: string }>(runSqlCmdQuery(verifyQ));
    ownerEmail = o?.Email ?? '';
  } else {
    const db = await getDbPool();
    const r = await db
      .request()
      .input('tenantId', sql.Int, tenantId)
      .input('prefix', sql.NVarChar, `${expectedEmailPrefix}%`)
      .query(`SELECT TOP 1 Email FROM AppUsers WHERE TenantID = @tenantId AND Email LIKE @prefix`);
    ownerEmail = r.recordset[0]?.Email ?? '';
  }
  if (!ownerEmail) {
    throw new Error(
      `deleteE2eTenantCascade: tenant ${tenantId} e2e kayıt e-postası (${expectedEmailPrefix}*) ile eşleşmiyor — silme iptal`,
    );
  }

  const statements = [
    `DELETE FROM AppointmentServiceLinks WHERE AppointmentID IN (SELECT AppointmentID FROM Appointments WHERE TenantID = ${tenantId})`,
    `DELETE FROM Appointments WHERE TenantID = ${tenantId}`,
    `DELETE FROM Feedbacks WHERE TenantID = ${tenantId}`,
    `DELETE FROM TenantBlockedPhones WHERE TenantID = ${tenantId}`,
    `DELETE FROM BusinessHours WHERE TenantID = ${tenantId}`,
    `DELETE FROM Holidays WHERE TenantId = ${tenantId}`,
    `DELETE FROM TransactionLogs WHERE TenantId = ${tenantId}`,
    `DELETE FROM AuditLogs WHERE TenantId = ${tenantId}`,
    `DELETE FROM UserOperationClaims WHERE UserId IN (SELECT AppUserID FROM AppUsers WHERE TenantID = ${tenantId})`,
    `DELETE FROM AppUsers WHERE TenantID = ${tenantId}`,
    `DELETE FROM Services WHERE TenantID = ${tenantId}`,
    `DELETE FROM Tenants WHERE TenantID = ${tenantId}`,
  ];

  const batch = `SET NOCOUNT ON; ${statements.join('; ')};`;
  if (useSqlCmd()) {
    runSqlCmdQuery(batch);
    return;
  }
  const db = await getDbPool();
  await db.request().query(batch);
}

/** İlk sektör ID (kayıt formu). */
export async function getFirstSectorId(): Promise<number> {
  const q = `SET NOCOUNT ON; SELECT TOP 1 SectorID FROM Sectors ORDER BY SectorID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER`;
  if (useSqlCmd()) {
    const o = parseSqlCmdJsonRow<{ SectorID?: number }>(runSqlCmdQuery(q));
    if (o?.SectorID) return Number(o.SectorID);
  }
  const db = await getDbPool();
  const r = await db.request().query(`SELECT TOP 1 SectorID FROM Sectors ORDER BY SectorID`);
  const id = r.recordset[0]?.SectorID;
  if (!id) throw new Error('Sectors tablosu boş');
  return Number(id);
}
