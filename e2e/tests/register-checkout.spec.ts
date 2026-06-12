/**
 * Gerekli env: E2E_API_URL, IYZICO_WEBHOOK_SECRET, E2E_DB_*, E2E_RUN_PANEL_TESTS=true
 * Kayıt: doğrudan API (WebUI SQL timeout riskini azaltır) + İyzico webhook + OTP panel.
 */
import { test, expect, request as playwrightRequest } from '@playwright/test';
import { loginWithOtpOnPage, PANEL_URL, expectAuthCookie } from '../helpers/auth';
import {
  assertDbWritable,
  deleteE2eTenantCascade,
  getFirstSectorId,
  getTenantSubscriptionState,
  requireDbConfigured,
} from '../helpers/db';
import { panelTestsEnabled } from '../helpers/e2e-config';
import { getEnvConfig, isReadonlyEnv } from '../helpers/env';
import { postIyzicoWebhook } from '../helpers/webhooks';

const IYZICO_SECRET = process.env.IYZICO_WEBHOOK_SECRET?.trim();
const VALID_TCKN = '10000000146';

let createdTenantId: number | null = null;

test.describe.configure({ retries: 1 });

async function registerBusinessViaApi(payload: {
  userFullName: string;
  userEmail: string;
  businessName: string;
  sectorID: number;
  phoneNumber: string;
  address: string;
  planType: string;
  billingCycle: string;
  identityNumber: string;
  birthYear: number;
}): Promise<{ tenantId: number; checkoutSkipped: boolean }> {
  const { apiBaseUrl, webUiBaseUrl } = getEnvConfig();
  const ctx = await playwrightRequest.newContext({ ignoreHTTPSErrors: true });
  const body = {
    ...payload,
    billingCycle: payload.billingCycle.charAt(0).toUpperCase() + payload.billingCycle.slice(1).toLowerCase(),
    paymentCallbackUrl: `${webUiBaseUrl}/Auth/PaymentCallback`,
  };

  let lastErr = '';
  for (let attempt = 0; attempt < 3; attempt++) {
    const res = await ctx.post(`${apiBaseUrl}/api/Auth/register-business`, {
      data: body,
      timeout: 300_000,
    });
    const text = await res.text();
    let json: Record<string, unknown> = {};
    try {
      json = JSON.parse(text) as Record<string, unknown>;
    } catch {
      json = { message: text };
    }

    const tenantId = Number(json.tenantId ?? json.TenantId ?? 0);
    const skipped = Boolean(json.paymentCheckoutSkipped ?? json.PaymentCheckoutSkipped);
    if (res.ok() && tenantId > 0) {
      await ctx.dispose();
      return { tenantId, checkoutSkipped: skipped };
    }

    lastErr = String(json.message ?? json.Message ?? text);
    if (/timeout expired|zaman aşımı/i.test(lastErr) && attempt < 2) {
      await new Promise((r) => setTimeout(r, 10_000));
      continue;
    }
    break;
  }

  await ctx.dispose();
  throw new Error(`register-business başarısız: ${lastErr}`);
}

async function bootstrapFirstStaffViaApi(
  tenantId: number,
  payload: {
    firstName: string;
    lastName: string;
    email: string;
    phoneNumber: string;
    specialization?: string;
  },
): Promise<void> {
  const { apiBaseUrl } = getEnvConfig();
  const ctx = await playwrightRequest.newContext({ ignoreHTTPSErrors: true });
  const res = await ctx.post(`${apiBaseUrl}/api/AppUsers/bootstrap-first-staff`, {
    data: {
      tenantId,
      firstName: payload.firstName,
      lastName: payload.lastName,
      email: payload.email,
      phoneNumber: payload.phoneNumber,
      specialization: payload.specialization ?? '',
    },
    timeout: 60_000,
  });
  const text = await res.text();
  await ctx.dispose();
  if (!res.ok()) {
    let msg = text;
    try {
      const j = JSON.parse(text) as { message?: string; Message?: string };
      msg = j.message ?? j.Message ?? text;
    } catch {
      /* ignore */
    }
    if (/zaten personel var/i.test(msg)) return;
    throw new Error(`bootstrap-first-staff başarısız (HTTP ${res.status()}): ${msg || text.slice(0, 200)}`);
  }
}

test.describe('Kayıt + ödeme aktivasyonu @destructive', () => {
  test.beforeAll(() => {
    test.skip(isReadonlyEnv(), 'Production readonly: kayıt testi kapalı');
    requireDbConfigured();
    test.skip(!IYZICO_SECRET, 'IYZICO_WEBHOOK_SECRET gerekli');
    test.skip(!panelTestsEnabled(), 'Panel OTP kapalı (E2E_RUN_PANEL_TESTS=true)');
  });

  test.afterAll(async () => {
    if (createdTenantId && createdTenantId > 0) {
      await deleteE2eTenantCascade(createdTenantId);
    }
  });

  test('API kayıt → webhook aktivasyon → OTP dashboard', async ({ page }) => {
    test.setTimeout(300_000);
    assertDbWritable();

    const ts = Date.now();
    const uniqueEmail = `e2e-reg-${ts}@example.com`;
    const uniquePhone = `05${String(300000000 + (ts % 899999999)).padStart(9, '0')}`.slice(0, 11);
    const plan = (process.env.E2E_REGISTER_PLAN ?? 'starter').trim();
    const cycle = (process.env.E2E_REGISTER_BILLING_CYCLE ?? 'monthly').trim().toLowerCase();
    const sectorId =
      Number(process.env.E2E_SECTOR_ID ?? '0') || (await getFirstSectorId());

    const { tenantId, checkoutSkipped } = await registerBusinessViaApi({
      userFullName: 'Esse Kayit Test',
      userEmail: uniqueEmail,
      businessName: `Esse Isletme ${ts}`,
      sectorID: sectorId,
      phoneNumber: uniquePhone,
      address: 'E2E Test Mahalle 1',
      planType: plan,
      billingCycle: cycle,
      identityNumber: VALID_TCKN,
      birthYear: 1990,
    });

    createdTenantId = tenantId;
    test.skip(
      checkoutSkipped,
      'İyzico kapalı (paymentCheckoutSkipped) — API appsettings IyzicoSettings.Enabled=true gerekli',
    );

    const before = await getTenantSubscriptionState(createdTenantId);
    const subscriptionRef = before.SubscriptionReferenceCode;
    expect(subscriptionRef, 'SubscriptionReferenceCode kayıt sonrası dolu olmalı').toBeTruthy();

    const paymentId = `e2e-reg-${ts}`;
    const { status, body } = await postIyzicoWebhook({
      eventType: 'payment.success',
      subscriptionReferenceCode: subscriptionRef,
      paymentId,
    });
    expect(status, body).toBe(200);

    await expect
      .poll(async () => {
        const t = await getTenantSubscriptionState(createdTenantId!);
        return t.IsActive && t.IsSubscriptionActive;
      })
      .toBeTruthy();

    await bootstrapFirstStaffViaApi(tenantId, {
      firstName: 'Esse',
      lastName: 'Kayit Test',
      email: uniqueEmail,
      phoneNumber: uniquePhone,
    });

    await loginWithOtpOnPage(page, uniquePhone);
    await page.waitForURL(`**${PANEL_URL}**`, { timeout: 60_000 });
    await expectAuthCookie(page);
    await expect(page.locator('#dashboard-page, .dashboard-page').first()).toBeVisible({
      timeout: 30_000,
    });
  });
});
