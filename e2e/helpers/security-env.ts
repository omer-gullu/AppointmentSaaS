import { request as playwrightRequest } from '@playwright/test';
import {
  ensureE2eTenantBillingActive,
  getManagerPhoneForTenant,
  getTenantE2eConfig,
  isTenantSuspendedInDb,
  provisionSecurityTestEnv,
  restoreTenantBilling,
  setTenantSubscriptionSuspended,
  snapshotTenantBilling,
} from './db';
import { getEnvConfig } from './env';
import { n8nAuthHeaders } from './n8n';

const API_REQUEST_TIMEOUT_MS = Number(process.env.E2E_API_REQUEST_TIMEOUT_MS ?? 90_000);

let securityTestRestore: (() => Promise<void>) | null = null;

function apiRequestContext() {
  return playwrightRequest.newContext({
    ignoreHTTPSErrors: true,
    timeout: API_REQUEST_TIMEOUT_MS,
  });
}

export type SecurityEnvConfig = {
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

function envTrim(key: string): string {
  return process.env[key]?.trim() ?? '';
}

function assignEnv(pairs: Record<string, string>): void {
  for (const [key, value] of Object.entries(pairs)) {
    if (value) process.env[key] = value;
  }
}

/** Çapraz-kiracı ve pasif tenant API testleri için zorunlu env. */
export function missingSecurityEnv(): string[] {
  const missing: string[] = [];
  if (!envTrim('E2E_TENANT_A_ID')) missing.push('E2E_TENANT_A_ID');
  if (!envTrim('E2E_TENANT_A_TOKEN')) missing.push('E2E_TENANT_A_TOKEN');
  if (!envTrim('E2E_TENANT_B_ID')) missing.push('E2E_TENANT_B_ID');
  if (!envTrim('E2E_TENANT_B_TOKEN')) missing.push('E2E_TENANT_B_TOKEN');
  if (!envTrim('E2E_TENANT_B_APPOINTMENT_ID')) missing.push('E2E_TENANT_B_APPOINTMENT_ID');
  if (!envTrim('E2E_PASSIVE_TENANT_ID')) missing.push('E2E_PASSIVE_TENANT_ID');
  if (!envTrim('E2E_PASSIVE_TENANT_TOKEN')) missing.push('E2E_PASSIVE_TENANT_TOKEN');
  if (!envTrim('E2E_PASSIVE_INSTANCE_NAME')) missing.push('E2E_PASSIVE_INSTANCE_NAME');
  if (!envTrim('E2E_PASSIVE_MANAGER_PHONE')) missing.push('E2E_PASSIVE_MANAGER_PHONE');
  return missing;
}

/**
 * E2E_TENANT_ID + DB'den Tenant B, randevu ve pasif tenant hazırlar.
 * Pasif tenant yoksa Tenant B geçici askıya alınır (afterAll'da geri yüklenir).
 */
async function ensurePassiveScenarioForTests(): Promise<void> {
  const tenantBId = Number(envTrim('E2E_TENANT_B_ID'));
  const tenantBToken = envTrim('E2E_TENANT_B_TOKEN');
  if (tenantBId <= 0 || !tenantBToken) {
    throw new Error('ensurePassiveScenarioForTests: E2E_TENANT_B_ID / E2E_TENANT_B_TOKEN eksik');
  }

  let snap: Awaited<ReturnType<typeof snapshotTenantBilling>> | null = null;
  if (!(await isTenantSuspendedInDb(tenantBId))) {
    snap = await snapshotTenantBilling(tenantBId);
    await setTenantSubscriptionSuspended(tenantBId, true);
  }

  const cfg = await getTenantE2eConfig(tenantBId);
  const managerPhone = await getManagerPhoneForTenant(tenantBId);
  if (!cfg?.instanceName || !managerPhone) {
    throw new Error(
      `ensurePassiveScenarioForTests: Tenant B (${tenantBId}) instance veya manager telefonu bulunamadi`,
    );
  }

  assignEnv({
    E2E_PASSIVE_TENANT_ID: String(tenantBId),
    E2E_PASSIVE_TENANT_TOKEN: tenantBToken,
    E2E_PASSIVE_INSTANCE_NAME: cfg.instanceName,
    E2E_PASSIVE_MANAGER_PHONE: managerPhone,
  });

  if (snap) {
    const prevRestore = securityTestRestore;
    securityTestRestore = async () => {
      await restoreTenantBilling(tenantBId, snap!);
      await ensureE2eTenantBillingActive(tenantBId).catch(() => {});
      if (prevRestore) await prevRestore();
    };
  }
}

export async function bootstrapSecurityEnvFromDb(): Promise<void> {
  if (!envTrim('E2E_TENANT_A_ID') && envTrim('E2E_TENANT_ID')) {
    process.env.E2E_TENANT_A_ID = envTrim('E2E_TENANT_ID');
  }
  if (!envTrim('E2E_TENANT_A_TOKEN')) {
    const token = envTrim('E2E_N8N_TOKEN') || envTrim('N8N_AUTH_TOKEN');
    if (token) process.env.E2E_TENANT_A_TOKEN = token;
  }

  if (missingSecurityEnv().length > 0) {
    const tenantAId = Number(envTrim('E2E_TENANT_A_ID'));
    const tenantAToken = envTrim('E2E_TENANT_A_TOKEN');
    const provisioned = await provisionSecurityTestEnv(tenantAId, tenantAToken);
    if (provisioned) {
      securityTestRestore = provisioned.restore;
      const r = provisioned.row;
      assignEnv({
        E2E_TENANT_A_ID: String(r.tenantAId),
        E2E_TENANT_A_TOKEN: r.tenantAToken,
        E2E_TENANT_B_ID: String(r.tenantBId),
        E2E_TENANT_B_TOKEN: r.tenantBToken,
        E2E_TENANT_B_APPOINTMENT_ID: String(r.tenantBAppointmentId),
        E2E_PASSIVE_TENANT_ID: String(r.passiveTenantId),
        E2E_PASSIVE_TENANT_TOKEN: r.passiveTenantToken,
        E2E_PASSIVE_INSTANCE_NAME: r.passiveInstanceName,
        E2E_PASSIVE_MANAGER_PHONE: r.passiveManagerPhone,
      });
    }
  }

  if (missingSecurityEnv().length > 0) return;

  await ensurePassiveScenarioForTests();
}

export async function restoreSecurityTestEnv(): Promise<void> {
  if (securityTestRestore) {
    await securityTestRestore();
    securityTestRestore = null;
  }
}

export function getSecurityEnv(): SecurityEnvConfig {
  return {
    tenantAId: Number(process.env.E2E_TENANT_A_ID),
    tenantAToken: process.env.E2E_TENANT_A_TOKEN!.trim(),
    tenantBId: Number(process.env.E2E_TENANT_B_ID),
    tenantBToken: process.env.E2E_TENANT_B_TOKEN!.trim(),
    tenantBAppointmentId: Number(process.env.E2E_TENANT_B_APPOINTMENT_ID),
    passiveTenantId: Number(process.env.E2E_PASSIVE_TENANT_ID),
    passiveTenantToken: process.env.E2E_PASSIVE_TENANT_TOKEN!.trim(),
    passiveInstanceName: process.env.E2E_PASSIVE_INSTANCE_NAME!.trim(),
    passiveManagerPhone: process.env.E2E_PASSIVE_MANAGER_PHONE!.trim(),
  };
}

export async function apiCallAsTenant(
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  path: string,
  tenantId: number,
  token: string,
  options?: { body?: Record<string, unknown>; query?: Record<string, string> },
): Promise<{ status: number; json: unknown; text: string }> {
  const { apiBaseUrl } = getEnvConfig();
  const qs = new URLSearchParams(options?.query ?? {}).toString();
  const url = `${apiBaseUrl}${path}${qs ? `?${qs}` : ''}`;
  const headers = {
    ...n8nAuthHeaders(tenantId, token),
    ...(options?.body ? { 'Content-Type': 'application/json' } : {}),
  };

  const ctx = await apiRequestContext();
  let response;
  switch (method) {
    case 'GET':
      response = await ctx.get(url, { headers });
      break;
    case 'POST':
      response = await ctx.post(url, { headers, data: options?.body ?? {} });
      break;
    case 'PUT':
      response = await ctx.put(url, { headers, data: options?.body ?? {} });
      break;
    case 'DELETE':
      response = await ctx.delete(url, { headers });
      break;
    default:
      await ctx.dispose();
      throw new Error(`Unsupported method: ${method}`);
  }
  const text = await response.text();
  let json: unknown = {};
  try {
    json = JSON.parse(text);
  } catch {
    json = { raw: text };
  }
  await ctx.dispose();
  return { status: response.status(), json, text };
}
