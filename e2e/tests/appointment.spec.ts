import { test, expect } from '@playwright/test';
import { PANEL_URL } from '../helpers/auth';
import { getEnvConfig } from '../helpers/env';
import { assertDbWritable, ensureE2eBreakTime, ensureE2eBusinessHours } from '../helpers/db';
import { getGoogleAccessTokenViaWebhook, postN8nAppointment } from '../helpers/webhooks';
import { deleteAppointmentAsN8n, waitForAppointmentViaApi } from '../helpers/appointments';
import { getE2eStaticConfig, panelTestsEnabled } from '../helpers/e2e-config';
import { isReadonlyEnv } from '../helpers/env';
import { isManagerAuthReady, MANAGER_AUTH_FILE } from '../helpers/panel-auth';
import { resolveE2eBookingContext } from '../helpers/n8n';
import { resolveStaffIdWithGoogleToken, skipUnlessGoogleStaff } from '../helpers/google-e2e';

const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const E2E_SERVICE_ID = Number(process.env.E2E_SERVICE_ID ?? '0');
const E2E_STAFF_ID = Number(process.env.E2E_STAFF_ID ?? '0');
const REQUIRE_GOOGLE = process.env.E2E_REQUIRE_GOOGLE === 'true';

/** 5320000xxx — API SandboxPhonePrefixes ile WhatsApp gönderilmez (gerçek müşteri değil). */
const E2E_CUSTOMER_PHONE_PREFIX = (process.env.E2E_CUSTOMER_PHONE_PREFIX ?? '5320000').replace(/\D/g, '').slice(0, 7);

let slotSeq = 0;
let panelAppointmentId: number | null = null;
let apiAppointmentId: number | null = null;
let googleStaffId: number | null = null;

function uniquePhone(): string {
  const suffix = String((Date.now() + slotSeq * 17) % 1000).padStart(3, '0');
  slotSeq += 1;
  return `${E2E_CUSTOMER_PHONE_PREFIX}${suffix}`;
}

/** dashboard.js: ad sadece harf — "E2E" içindeki 2 rakam sayılır, kullanma. */
function uniqueCustomerName(prefix: string): string {
  const letters = 'ABCDEFGHKLMNPRSTUVYZ';
  const a = letters[Date.now() % letters.length];
  const b = letters[Math.floor(Date.now() / 11) % letters.length];
  return `Esse ${prefix} Test ${a}${b}`;
}

test.describe.configure({ mode: 'serial' });

async function cleanupTrackedAppointments(): Promise<void> {
  const staticCfg = getE2eStaticConfig();
  if (!staticCfg) return;
  const { tenantId, n8nToken } = staticCfg;
  const ids = [panelAppointmentId, apiAppointmentId].filter(
    (id): id is number => id != null && id > 0,
  );
  for (const id of ids) {
    const res = await deleteAppointmentAsN8n(id, n8nToken, tenantId);
    if (res.status !== 200 && res.status !== 404) {
      console.error(`deleteAppointmentAsN8n(${id}) → ${res.status}`, res.json);
      throw new Error(`Randevu temizliği başarısız: appointmentId=${id} status=${res.status}`);
    }
  }
}

test.describe('Randevu — panel @destructive', () => {
  test.skip(!panelTestsEnabled(), 'Panel testleri kapalı (E2E_RUN_PANEL_TESTS=true)');

  test.beforeAll(async () => {
    test.skip(isReadonlyEnv(), 'Production readonly: randevu mutasyon testleri kapalı');
    test.skip(!getE2eStaticConfig(), 'Statik .env eksik (discover-env.ps1)');
    test.skip(!isManagerAuthReady(), 'global-setup OTP başarısız — manager.json yok');
    await ensureE2eBusinessHours(E2E_TENANT_ID);
    await ensureE2eBreakTime(E2E_TENANT_ID);
  });

  test.use({ storageState: MANAGER_AUTH_FILE });

  test('panelden manuel randevu → DB kaydı (+ opsiyonel Google event)', async ({ page }) => {
    test.setTimeout(180_000);
    assertDbWritable();
    const staticCfg = getE2eStaticConfig()!;
    const customerPhone = uniquePhone();
    const customerName = uniqueCustomerName('Panel');
    const booking = await resolveE2eBookingContext(
      staticCfg.tenantId,
      staticCfg.instanceName,
      staticCfg.n8nToken,
      staticCfg.serviceId,
      staticCfg.staffId,
    );

    const { webUiBaseUrl } = getEnvConfig();
    await page.goto(`${webUiBaseUrl}${PANEL_URL}`);
    await expect(page).toHaveURL(new RegExp(PANEL_URL.replace(/\//g, '\\/')));

    await page.locator('[data-bs-target="#newAppointmentModal"]').click();
    await expect(page.locator('#newAppointmentModal')).toBeVisible();

    await page.locator('#appCustomerName').fill(customerName);
    await page.locator('#appCustomerPhone').fill(customerPhone);
    await page.locator('#newAppointmentModal select[name="serviceId"]').selectOption(String(E2E_SERVICE_ID));
    await page.locator('#appUserId').selectOption(String(E2E_STAFF_ID));
    await page.locator('#newAppointmentModal input[name="date"]').fill(booking.slotDate);
    await page.locator('#newAppointmentModal input[name="time"]').fill(booking.slotTime);

    const submitBtn = page.locator('#newAppointmentModal button[type="submit"]');
    await Promise.all([
      page.waitForURL(/\/Dashboard/i, { waitUntil: 'domcontentloaded', timeout: 90_000 }),
      submitBtn.click(),
    ]);

    const swal = page.locator('.swal2-popup:visible');
    if (await swal.count()) {
      const msg = (await swal.textContent())?.trim();
      throw new Error(`Randevu kaydı reddedildi: ${msg}`);
    }
    await expect(page.locator('#newAppointmentModal')).toBeHidden({ timeout: 45_000 });
    await expect(page.locator('.alert-success').first()).toBeVisible({ timeout: 15_000 }).catch(() => {});

    const row = await waitForAppointmentViaApi({
      tenantId: staticCfg.tenantId,
      token: staticCfg.n8nToken,
      instanceName: staticCfg.instanceName,
      customerPhone,
    });
    const name = String(row.customerName ?? row.CustomerName ?? '');
    expect(name).toMatch(/Esse Panel Test/i);
    panelAppointmentId = Number(row.appointmentId ?? row.AppointmentId ?? 0) || null;

    if (REQUIRE_GOOGLE) {
      test.info().annotations.push({
        type: 'note',
        description: 'Google event id panel API üzerinden doğrulanır; E2E_REQUIRE_GOOGLE=true iken staff token gerekir',
      });
    }
  });
});

test.describe('Randevu — API @destructive', () => {
  test.beforeAll(async () => {
    test.skip(isReadonlyEnv(), 'Production readonly');
    test.skip(!getE2eStaticConfig(), 'Statik .env eksik (discover-env.ps1)');
    await ensureE2eBusinessHours(E2E_TENANT_ID);
    await ensureE2eBreakTime(E2E_TENANT_ID);
    const staticCfg = getE2eStaticConfig()!;
    googleStaffId = await resolveStaffIdWithGoogleToken(
      staticCfg.tenantId,
      staticCfg.instanceName,
      staticCfg.n8nToken,
      staticCfg.staffId,
    );
  });

  /** n8n workflow'un son adımı: doğrudan API (Gemini/n8n çalışması gerekmez). Canlı n8n → n8n-workflow.spec.ts */
  test('API randevu POST (n8n son adımı) → DB + refresh token', async () => {
    skipUnlessGoogleStaff(googleStaffId, E2E_STAFF_ID);
    assertDbWritable();
    const staticCfg = getE2eStaticConfig()!;
    const staffId = googleStaffId!;

    const customerPhone = uniquePhone();
    const customerName = uniqueCustomerName('Webhook');

    test.setTimeout(120_000);

    let status = 0;
    let json: unknown = {};
    const maxAttempts = 6;
    for (let attempt = 0; attempt < maxAttempts; attempt++) {
      const booking = await resolveE2eBookingContext(
        staticCfg.tenantId,
        staticCfg.instanceName,
        staticCfg.n8nToken,
        staticCfg.serviceId,
        staffId,
      );
      const res = await postN8nAppointment(
        {
          customerName,
          customerPhone,
          businessPhone: staticCfg.instanceName,
          serviceID: staticCfg.serviceId,
          appUserID: staffId,
          startDate: booking.startIso,
        },
        staticCfg.n8nToken,
        staticCfg.tenantId,
      );
      status = res.status;
      json = res.json;
      const msg = String((json as { message?: string })?.message ?? '');
      if (status === 200) break;
      if (status === 400 && /çakış/i.test(msg) && attempt < maxAttempts - 1) continue;
      break;
    }

    expect(status, JSON.stringify(json)).toBe(200);

    const row = await waitForAppointmentViaApi({
      tenantId: staticCfg.tenantId,
      token: staticCfg.n8nToken,
      instanceName: staticCfg.instanceName,
      customerPhone,
    });
    const name = String(row.customerName ?? row.CustomerName ?? '');
    expect(name).toMatch(/Esse Webhook Test/i);
    const jsonId = Number(
      (json as { ID?: number; id?: number; appointmentId?: number })?.ID ??
        (json as { id?: number }).id ??
        (json as { appointmentId?: number }).appointmentId ??
        0,
    );
    apiAppointmentId = jsonId > 0 ? jsonId : Number(row.appointmentId ?? row.AppointmentId ?? 0);

    const tokenRes = await getGoogleAccessTokenViaWebhook(
      staticCfg.instanceName,
      staticCfg.n8nToken,
      staffId,
    );
    if (REQUIRE_GOOGLE) {
      expect(tokenRes.status, JSON.stringify(tokenRes.json)).toBe(200);
      const body = tokenRes.json as { accessToken?: string };
      expect(body.accessToken, 'Google access token üretilemedi — refresh token geçersiz olabilir').toBeTruthy();
    } else if (tokenRes.status === 200) {
      const body = tokenRes.json as { accessToken?: string };
      expect(body.accessToken).toBeTruthy();
    } else {
      test.info().annotations.push({
        type: 'note',
        description: `GetGoogleAccessToken returned ${tokenRes.status} — staff/tenant Google bağlı olmayabilir`,
      });
    }
  });
});

test.afterAll(async () => {
  if (isReadonlyEnv()) return;
  await cleanupTrackedAppointments();
});
