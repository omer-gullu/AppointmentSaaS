import { test, expect } from '@playwright/test';
import { loginWithOtpOnPage, PANEL_URL, expectAuthCookie } from '../helpers/auth';
import {
  assertDbWritable,
  dbConfigured,
  requireDbConfigured,
  ensureE2eTenantBillingActive,
  getTenantSubscriptionState,
} from '../helpers/db';
import { postIyzicoWebhook } from '../helpers/webhooks';
import { panelTestsEnabled } from '../helpers/e2e-config';
import { isReadonlyEnv } from '../helpers/env';

const E2E_PHONE = process.env.E2E_MANAGER_PHONE?.trim();
const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const IYZICO_SECRET = process.env.IYZICO_WEBHOOK_SECRET?.trim();

test.describe('Iyzico ödeme webhook @destructive', () => {
  test.beforeAll(() => {
    test.skip(isReadonlyEnv(), 'Production readonly: ödeme webhook testi kapalı');
    requireDbConfigured();
    test.skip(!IYZICO_SECRET, 'IYZICO_WEBHOOK_SECRET gerekli');
    test.skip(!E2E_TENANT_ID, 'E2E_TENANT_ID gerekli');
  });

  test('imzalı webhook → tenant active + panele giriş', async ({ page }) => {
    assertDbWritable();

    const before = await getTenantSubscriptionState(E2E_TENANT_ID);
    const subscriptionRef = before.SubscriptionReferenceCode ?? process.env.E2E_SUBSCRIPTION_REF?.trim();
    test.skip(!subscriptionRef, 'Tenant SubscriptionReferenceCode veya E2E_SUBSCRIPTION_REF gerekli');

    const paymentId = `e2e-pay-${Date.now()}`;
    const payload = {
      eventType: 'payment.success',
      subscriptionReferenceCode: subscriptionRef,
      paymentId,
    };

    const { status, body } = await postIyzicoWebhook(payload);
    expect(status, body).toBe(200);

    await expect
      .poll(async () => {
        const t = await getTenantSubscriptionState(E2E_TENANT_ID);
        return t.IsActive && t.IsSubscriptionActive;
      })
      .toBeTruthy();

    test.skip(!panelTestsEnabled(), 'Panel OTP kapalı (E2E_RUN_PANEL_TESTS=true)');
    test.skip(!E2E_PHONE, 'Ödeme sonrası UI: E2E_MANAGER_PHONE gerekli');

    await ensureE2eTenantBillingActive(E2E_TENANT_ID);
    await loginWithOtpOnPage(page, E2E_PHONE!);
    await page.waitForURL(`**${PANEL_URL}**`, { timeout: 30_000 });
    await expectAuthCookie(page);
    await expect(page.locator('#dashboard-page, .dashboard-page').first()).toBeVisible();
  });
});
