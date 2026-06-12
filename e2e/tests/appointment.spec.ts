import { test, expect } from '@playwright/test';
import { PANEL_URL } from '../helpers/auth';
import { getEnvConfig } from '../helpers/env';
import {
  assertDbWritable,
  dbConfigured,
  ensureE2eBreakTime,
  ensureE2eBusinessHours,
  requireDbConfigured,
  waitForAppointment,
  staffHasGoogleRefreshToken,
} from '../helpers/db';
import { getGoogleAccessTokenViaWebhook, postN8nAppointment } from '../helpers/webhooks';
import { deleteAppointmentAsN8n } from '../helpers/appointments';
import { getE2eStaticConfig, panelTestsEnabled } from '../helpers/e2e-config';
import { isReadonlyEnv } from '../helpers/env';
import { isManagerAuthReady, MANAGER_AUTH_FILE } from '../helpers/panel-auth';

const E2E_PHONE = process.env.E2E_MANAGER_PHONE?.trim();
const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const E2E_INSTANCE = process.env.E2E_INSTANCE_NAME?.trim();
const E2E_N8N_TOKEN = process.env.E2E_N8N_TOKEN?.trim() ?? process.env.N8N_AUTH_TOKEN?.trim();
const E2E_SERVICE_ID = Number(process.env.E2E_SERVICE_ID ?? '0');
const E2E_STAFF_ID = Number(process.env.E2E_STAFF_ID ?? '0');
const REQUIRE_GOOGLE = process.env.E2E_REQUIRE_GOOGLE === 'true';

/** 5320000xxx — API SandboxPhonePrefixes ile WhatsApp gönderilmez (gerçek müşteri değil). */
const E2E_CUSTOMER_PHONE_PREFIX = (process.env.E2E_CUSTOMER_PHONE_PREFIX ?? '5320000').replace(/\D/g, '').slice(0, 7);

let slotSeq = 0;
let panelAppointmentId: number | null = null;
let apiAppointmentId: number | null = null;

function uniquePhone(): string {
  const suffix = String((Date.now() + slotSeq * 17) % 1000).padStart(3, '0');
  return `${E2E_CUSTOMER_PHONE_PREFIX}${suffix}`;
}

function pad2(n: number): string {
  return String(n).padStart(2, '0');
}

/** Yerel takvim (panel + API unspecifed DateTime.Parse ile uyumlu; UTC slice kullanma). */
function localYmd(d: Date): string {
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}

function localHm(d: Date): string {
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}`;
}

/** dashboard.js: ad sadece harf — "E2E" içindeki 2 rakam sayılır, kullanma. */
function uniqueCustomerName(prefix: string): string {
  const letters = 'ABCDEFGHKLMNPRSTUVYZ';
  const a = letters[Date.now() % letters.length];
  const b = letters[Math.floor(Date.now() / 11) % letters.length];
  return `Esse ${prefix} Test ${a}${b}`;
}

/** Önceki E2E kayıtlarıyla çakışmayı azalt: 14–90 gün ileri, 5 dk grid, seq + zaman. */
function nextSlot(): { date: string; time: string; iso: string } {
  slotSeq += 1;
  const now = Date.now();
  const d = new Date();
  const dayOffset = 14 + ((Math.floor(now / 1000) + slotSeq * 37) % 76);
  const slotIndex = (Math.floor(now / 300_000) + slotSeq * 41) % 96;
  const hour = 8 + Math.floor(slotIndex / 12);
  const minute = (slotIndex % 12) * 5;
  d.setDate(d.getDate() + dayOffset);
  while (d.getDay() === 0) d.setDate(d.getDate() + 1);
  d.setHours(hour, minute, 0, 0);
  const date = localYmd(d);
  const time = localHm(d);
  const iso = `${date}T${time}:00`;
  return { date, time, iso };
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
    requireDbConfigured();
    test.skip(!getE2eStaticConfig(), 'Statik .env eksik (discover-env.ps1)');
    test.skip(!isManagerAuthReady(), 'global-setup OTP başarısız — manager.json yok');
    await ensureE2eBusinessHours(E2E_TENANT_ID);
    await ensureE2eBreakTime(E2E_TENANT_ID);
  });

  test.use({ storageState: MANAGER_AUTH_FILE });

  test('panelden manuel randevu → DB kaydı (+ opsiyonel Google event)', async ({ page }) => {
    test.setTimeout(180_000);
    assertDbWritable();
    const customerPhone = uniquePhone();
    const customerName = uniqueCustomerName('Panel');
    const slot = nextSlot();

    const { webUiBaseUrl } = getEnvConfig();
    await page.goto(`${webUiBaseUrl}${PANEL_URL}`);
    await expect(page).toHaveURL(new RegExp(PANEL_URL.replace(/\//g, '\\/')));

    await page.locator('[data-bs-target="#newAppointmentModal"]').click();
    await expect(page.locator('#newAppointmentModal')).toBeVisible();

    await page.locator('#appCustomerName').fill(customerName);
    await page.locator('#appCustomerPhone').fill(customerPhone);
    await page.locator('#newAppointmentModal select[name="serviceId"]').selectOption(String(E2E_SERVICE_ID));
    await page.locator('#appUserId').selectOption(String(E2E_STAFF_ID));
    await page.locator('#newAppointmentModal input[name="date"]').fill(slot.date);
    await page.locator('#newAppointmentModal input[name="time"]').fill(slot.time);

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

    const row = await waitForAppointment(E2E_TENANT_ID, customerPhone.slice(-8));
    expect(row.CustomerName).toMatch(/Esse Panel Test/i);
    panelAppointmentId = row.AppointmentID;

    if (REQUIRE_GOOGLE) {
      const hasRefresh = await staffHasGoogleRefreshToken(E2E_STAFF_ID);
      test.skip(!hasRefresh, 'Personelde GoogleRefreshToken yok');
      expect(row.GoogleEventID, 'Google Calendar event id bekleniyor').toBeTruthy();
    }
  });
});

test.describe('Randevu — API @destructive', () => {
  test.beforeAll(async () => {
    test.skip(isReadonlyEnv(), 'Production readonly');
    requireDbConfigured();
    test.skip(!getE2eStaticConfig(), 'Statik .env eksik (discover-env.ps1)');
    await ensureE2eBusinessHours(E2E_TENANT_ID);
    await ensureE2eBreakTime(E2E_TENANT_ID);
  });

  /** n8n workflow'un son adımı: doğrudan API (Gemini/n8n çalışması gerekmez). Canlı n8n → n8n-workflow.spec.ts */
  test('API randevu POST (n8n son adımı) → DB + refresh token', async () => {
    assertDbWritable();
    const staticCfg = getE2eStaticConfig()!;

    const customerPhone = uniquePhone();
    const customerName = uniqueCustomerName('Webhook');

    test.setTimeout(120_000);

    let status = 0;
    let json: unknown = {};
    const maxAttempts = 6;
    for (let attempt = 0; attempt < maxAttempts; attempt++) {
      const slot = nextSlot();
      const res = await postN8nAppointment(
        {
          customerName,
          customerPhone,
          businessPhone: staticCfg.instanceName,
          serviceID: staticCfg.serviceId,
          appUserID: staticCfg.staffId,
          startDate: slot.iso,
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

    const row = await waitForAppointment(staticCfg.tenantId, customerPhone.slice(-8));
    expect(row.CustomerName).toMatch(/Esse Webhook Test/i);
    const jsonId = Number(
      (json as { ID?: number; id?: number; appointmentId?: number })?.ID ??
        (json as { id?: number }).id ??
        (json as { appointmentId?: number }).appointmentId ??
        0,
    );
    apiAppointmentId = jsonId > 0 ? jsonId : row.AppointmentID;

    const tokenRes = await getGoogleAccessTokenViaWebhook(
      staticCfg.instanceName,
      staticCfg.n8nToken,
      staticCfg.staffId,
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
