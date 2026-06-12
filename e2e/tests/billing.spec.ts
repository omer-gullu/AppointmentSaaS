import { test, expect } from '@playwright/test';
import { PANEL_URL } from '../helpers/auth';
import { missingBillingApiEnv, missingBillingPanelEnv } from '../helpers/billing-env';
import { getEnvConfig } from '../helpers/env';
import { ensurePanelSession, isManagerAuthReady, MANAGER_AUTH_FILE } from '../helpers/panel-auth';
import {
  assertDbWritable,
  ensureE2eTenantBillingActive,
  getTenantSubscriptionState,
  restoreTenantBilling,
  setTenantPendingPlanChange,
  snapshotTenantBilling,
  type TenantBillingSnapshot,
} from '../helpers/db';
import { postIyzicoWebhook } from '../helpers/webhooks';

const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const E2E_PHONE = process.env.E2E_MANAGER_PHONE?.trim();
const IYZICO_SECRET = process.env.IYZICO_WEBHOOK_SECRET?.trim();

test.describe('Abonelik webhook (API) @destructive @api', () => {
  let billingSnap: TenantBillingSnapshot | null = null;

  test.beforeAll(async () => {
    const missing = missingBillingApiEnv();
    test.skip(missing.length > 0, `Billing API atlandı — eksik: ${missing.join('; ')}`);
    assertDbWritable();
    await ensureE2eTenantBillingActive(E2E_TENANT_ID);
    billingSnap = await snapshotTenantBilling(E2E_TENANT_ID);
  });

  test.afterAll(async () => {
    if (billingSnap && E2E_TENANT_ID > 0) {
      await restoreTenantBilling(E2E_TENANT_ID, billingSnap);
      await ensureE2eTenantBillingActive(E2E_TENANT_ID);
    }
  });

  test('payment.failed → yenileme askısı (IsSubscriptionActive=false)', async () => {
    const state = await getTenantSubscriptionState(E2E_TENANT_ID);
    const subscriptionRef =
      state.SubscriptionReferenceCode ?? process.env.E2E_SUBSCRIPTION_REF?.trim();
    test.skip(!subscriptionRef, 'SubscriptionReferenceCode veya E2E_SUBSCRIPTION_REF gerekli');
    const ref = subscriptionRef as string;

    await restoreTenantBilling(E2E_TENANT_ID, {
      ...(billingSnap as TenantBillingSnapshot),
      IsActive: true,
      IsSubscriptionActive: true,
      PendingPlanType: null,
      PendingBillingCycle: null,
      PendingCheckoutToken: null,
      PreviousSubscriptionReferenceCode: null,
      SubscriptionReferenceCode: ref,
    });

    const { status } = await postIyzicoWebhook({
      eventType: 'payment.failed',
      subscriptionReferenceCode: subscriptionRef,
      paymentId: `e2e-fail-renew-${Date.now()}`,
    });
    expect(status).toBe(200);

    await expect
      .poll(async () => {
        const t = await getTenantSubscriptionState(E2E_TENANT_ID);
        return !t.IsSubscriptionActive;
      })
      .toBeTruthy();
  });

  test('payment.failed + pending plan → askı yok, pending temizlenir', async () => {
    const oldRef = `e2e-old-${Date.now()}`;
    const newCheckoutRef = `e2e-new-${Date.now()}`;
    await setTenantPendingPlanChange(E2E_TENANT_ID, {
      pendingPlanType: 'Pro',
      pendingBillingCycle: 'Monthly',
      pendingCheckoutToken: `e2e-pending-${Date.now()}`,
      previousSubscriptionReferenceCode: oldRef,
      subscriptionReferenceCode: newCheckoutRef,
    });

    try {
      const { status } = await postIyzicoWebhook({
        eventType: 'subscription.order.failure',
        subscriptionReferenceCode: newCheckoutRef,
        paymentId: `e2e-fail-pending-${Date.now()}`,
      });
      expect(status).toBe(200);

      const after = await snapshotTenantBilling(E2E_TENANT_ID);
      expect(after.IsSubscriptionActive).toBe(true);
      expect(after.IsActive).toBe(true);
      expect(after.PendingPlanType).toBeNull();
      expect(after.PendingCheckoutToken).toBeNull();
      expect(after.SubscriptionReferenceCode).toBe(oldRef);
    } finally {
      if (billingSnap) await restoreTenantBilling(E2E_TENANT_ID, billingSnap);
    }
  });
});

test.describe('Abonelik paneli', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeAll(() => {
    const missing = missingBillingPanelEnv();
    test.skip(missing.length > 0, `Billing panel atlandı — eksik: ${missing.join('; ')}`);
    test.skip(!isManagerAuthReady(), 'global-setup OTP başarısız — manager.json yok');
  });

  test.use({ storageState: MANAGER_AUTH_FILE });

  test('ChangePlan sayfası — giriş sonrası plan kartları', async ({ page }) => {
    const { webUiBaseUrl } = getEnvConfig();
    await page.goto(`${webUiBaseUrl}/Pricing/ChangePlan`);
    await expect(page.locator('.pricing-h1').first()).toContainText(/güncelleyin/i);
    await expect(page.locator('.plan-select-btn, .pcard-btn').first()).toBeVisible();
  });

  test('Dashboard — abonelik kartında plan değiştir linki', async ({ page }) => {
    await ensurePanelSession(page, PANEL_URL);

    const changePlanLink = page.locator('a[href="/Pricing/ChangePlan"]').first();
    await expect(changePlanLink).toBeVisible({ timeout: 15_000 });
    await expect(changePlanLink).toContainText(/plan/i);
  });
});
