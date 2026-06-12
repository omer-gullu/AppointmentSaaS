/**
 * Gerekli env (discover-env veya manuel):
 * E2E_TENANT_A_ID, E2E_TENANT_A_TOKEN
 * E2E_TENANT_B_ID, E2E_TENANT_B_TOKEN, E2E_TENANT_B_APPOINTMENT_ID
 * E2E_PASSIVE_TENANT_ID, E2E_PASSIVE_TENANT_TOKEN, E2E_PASSIVE_INSTANCE_NAME, E2E_PASSIVE_MANAGER_PHONE
 * E2E_DB_* (beforeAll)
 *
 * Veri: STATİK env — mutasyon yok (403 beklenir).
 */
import { test, expect } from '@playwright/test';
import { requireDbConfigured } from '../helpers/db';
import { isReadonlyEnv } from '../helpers/env';
import {
  apiCallAsTenant,
  bootstrapSecurityEnvFromDb,
  getSecurityEnv,
  missingSecurityEnv,
  restoreSecurityTestEnv,
} from '../helpers/security-env';

function securityEnvSkipReason(): string | null {
  const missing = missingSecurityEnv();
  if (missing.length === 0) return null;
  return [
    'Güvenlik testleri için e2e/.env içinde şu değişkenler eksik:',
    missing.join(', '),
    '',
    'Gereksinimler: E2E_TENANT_ID + DB’de ikinci bir tenant (ApiKey + instance). Pasif yoksa B geçici askıya alınır.',
    'Öneri: cd e2e/scripts && .\\discover-env.ps1 veya ikinci işletme kaydı oluşturun.',
  ].join('\n');
}

test.describe('Tenant izolasyonu @destructive @api', () => {
  test.beforeAll(async () => {
    test.skip(isReadonlyEnv(), 'Production readonly: güvenlik testleri kapalı');
    requireDbConfigured();
    await bootstrapSecurityEnvFromDb();
    const skipReason = securityEnvSkipReason();
    test.skip(!!skipReason, skipReason ?? '');
  });

  test('cross-tenant: Tenant A, Tenant B randevusunu silemez (403)', async () => {
    const env = getSecurityEnv();
    const del = await apiCallAsTenant(
      'DELETE',
      `/api/Appointments/${env.tenantBAppointmentId}`,
      env.tenantAId,
      env.tenantAToken,
    );
    expect(del.status, del.text).toBe(403);
  });

  test('cross-tenant: Tenant A, Tenant B personel listesine erişemez (403)', async () => {
    const env = getSecurityEnv();
    const res = await apiCallAsTenant(
      'GET',
      `/api/AppUsers/staff/${env.tenantBId}`,
      env.tenantAId,
      env.tenantAToken,
    );
    expect(res.status, res.text).toBe(403);
  });

  test('cross-tenant: Tenant A, Tenant B hizmetlerine erişemez (403)', async () => {
    const env = getSecurityEnv();
    const res = await apiCallAsTenant(
      'GET',
      `/api/Services/tenant/${env.tenantBId}`,
      env.tenantAId,
      env.tenantAToken,
    );
    expect(res.status, res.text).toBe(403);
  });

  test('pasif tenant: GetContextByInstance → 403', async () => {
    const env = getSecurityEnv();
    const res = await apiCallAsTenant(
      'GET',
      '/api/Tenants/GetContextByInstance',
      env.passiveTenantId,
      env.passiveTenantToken,
      { query: { instanceName: env.passiveInstanceName } },
    );
    expect(res.status, res.text).toBe(403);
  });

  // Pasif API: GetContext 403 yeterli. generate-otp askıda Iyzico reconcile ile
  // tenant'ı geçici aktifleştirebildiği için ayrı OTP assert güvenilir değil.
});

test.afterAll(async () => {
  await restoreSecurityTestEnv();
});
