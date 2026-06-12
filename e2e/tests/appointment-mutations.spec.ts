import { test, expect } from '@playwright/test';
import { PANEL_URL } from '../helpers/auth';
import { isManagerAuthReady, MANAGER_AUTH_FILE } from '../helpers/panel-auth';
import {
  assertAppointmentFields,
  createAppointmentAsN8n,
  deleteAppointmentAsN8n,
  isoToPanelDate,
  isoToPanelTime,
  resolveAlternateServiceAndStaff,
  shiftSlotIso,
  updateAppointmentAsN8n,
} from '../helpers/appointments';
import {
  assertDbWritable,
  appointmentExists,
  ensureE2eBusinessHours,
  requireDbConfigured,
} from '../helpers/db';
import { getE2eStaticConfig, panelTestsEnabled } from '../helpers/e2e-config';
import { getN8nAuthToken, resolveE2eBookingContext } from '../helpers/n8n';
import { isReadonlyEnv } from '../helpers/env';

const E2E_PHONE = process.env.E2E_MANAGER_PHONE?.trim();
const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const E2E_INSTANCE = process.env.E2E_INSTANCE_NAME?.trim() ?? '';
const E2E_SERVICE_ID = Number(process.env.E2E_SERVICE_ID ?? '0');
const E2E_STAFF_ID = Number(process.env.E2E_STAFF_ID ?? '0');
const CUSTOMER_PREFIX = (process.env.E2E_CUSTOMER_PHONE_PREFIX ?? '5320000').replace(/\D/g, '').slice(0, 7);

let slotSeq = 0;

function uniquePhone(): string {
  slotSeq += 1;
  return `${CUSTOMER_PREFIX}${String((Date.now() + slotSeq * 13) % 1000).padStart(3, '0')}`;
}

function uniqueName(prefix: string): string {
  const letters = 'ABCDEFGHKLMNPRSTUVYZ';
  return `Esse ${prefix} Mut ${letters[slotSeq % letters.length]}${letters[(slotSeq + 3) % letters.length]}`;
}

test.describe.configure({ mode: 'serial' });

function skipUnlessMutationsEnv(): void {
  test.skip(isReadonlyEnv(), 'readonly ortam');
  requireDbConfigured();
  test.skip(!E2E_TENANT_ID || !E2E_PHONE, 'E2E_TENANT_ID ve E2E_MANAGER_PHONE gerekli');
  test.skip(!E2E_SERVICE_ID || !E2E_STAFF_ID || !E2E_INSTANCE, 'E2E_INSTANCE_NAME, SERVICE, STAFF gerekli');
  test.skip(!getN8nAuthToken(), 'E2E_N8N_TOKEN gerekli');
}

test.describe('Randevu güncelle / sil — API @destructive', () => {
  test.beforeAll(async () => {
    skipUnlessMutationsEnv();
    await ensureE2eBusinessHours(E2E_TENANT_ID);
  });

  test('API: tarih/saat güncelle → DB', async () => {
    assertDbWritable();
    const token = getN8nAuthToken();
    const phone = uniquePhone();
    const name = uniqueName('ApiTime');
    const booking = await resolveE2eBookingContext(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: name,
        customerPhone: phone,
        businessPhone: E2E_INSTANCE,
        serviceID: E2E_SERVICE_ID,
        appUserID: E2E_STAFF_ID,
        startDate: booking.startIso,
      },
      token,
      E2E_TENANT_ID,
    );

    const newIso = shiftSlotIso(booking.startIso, 10);
    const upd = await updateAppointmentAsN8n(
      appointmentId,
      {
        customerName: name,
        customerPhone: phone,
        serviceID: E2E_SERVICE_ID,
        appUserID: E2E_STAFF_ID,
        startDate: newIso,
        businessPhone: E2E_INSTANCE,
      },
      token,
      E2E_TENANT_ID,
    );
    expect(upd.status, JSON.stringify(upd.json)).toBe(200);
    await assertAppointmentFields(appointmentId, { startIsoPrefix: newIso });

    await deleteAppointmentAsN8n(appointmentId, token, E2E_TENANT_ID);
    expect(await appointmentExists(appointmentId)).toBeFalsy();
  });

  test('API: hizmet + personel değiştir → DB', async () => {
    assertDbWritable();
    const token = getN8nAuthToken();
    const alt = await resolveAlternateServiceAndStaff(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );
    test.skip(!alt, 'İkinci hizmet/personel yok — tenant’a bir hizmet ve personel daha ekleyin');

    const phone = uniquePhone();
    const name = uniqueName('ApiAlt');
    const booking = await resolveE2eBookingContext(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: name,
        customerPhone: phone,
        businessPhone: E2E_INSTANCE,
        serviceID: E2E_SERVICE_ID,
        appUserID: E2E_STAFF_ID,
        startDate: booking.startIso,
      },
      token,
      E2E_TENANT_ID,
    );

    // Yeni personel için müsait slot (aynı saat → 409 çakışma)
    const altBooking = await resolveE2eBookingContext(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      alt!.serviceId,
      alt!.staffId,
    );

    let upd = await updateAppointmentAsN8n(
      appointmentId,
      {
        customerName: name,
        customerPhone: phone,
        serviceID: alt!.serviceId,
        appUserID: alt!.staffId,
        startDate: altBooking.startIso,
        businessPhone: E2E_INSTANCE,
      },
      token,
      E2E_TENANT_ID,
    );
    if (upd.status === 409) {
      const retryIso = shiftSlotIso(altBooking.startIso, 15);
      upd = await updateAppointmentAsN8n(
        appointmentId,
        {
          customerName: name,
          customerPhone: phone,
          serviceID: alt!.serviceId,
          appUserID: alt!.staffId,
          startDate: retryIso,
          businessPhone: E2E_INSTANCE,
        },
        token,
        E2E_TENANT_ID,
      );
    }
    expect(upd.status, JSON.stringify(upd.json)).toBe(200);
    await assertAppointmentFields(appointmentId, {
      serviceId: alt!.serviceId,
      staffId: alt!.staffId,
    });

    await deleteAppointmentAsN8n(appointmentId, token, E2E_TENANT_ID);
  });

  test('API: randevu sil (n8n randevu_sil aracı)', async () => {
    assertDbWritable();
    const token = getN8nAuthToken();
    const phone = uniquePhone();
    const booking = await resolveE2eBookingContext(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: uniqueName('ApiDel'),
        customerPhone: phone,
        businessPhone: E2E_INSTANCE,
        serviceID: E2E_SERVICE_ID,
        appUserID: E2E_STAFF_ID,
        startDate: booking.startIso,
      },
      token,
      E2E_TENANT_ID,
    );

    const del = await deleteAppointmentAsN8n(appointmentId, token, E2E_TENANT_ID);
    expect(del.status, JSON.stringify(del.json)).toBe(200);
    expect(await appointmentExists(appointmentId)).toBeFalsy();
  });
});

test.describe('Randevu güncelle / sil — panel @destructive', () => {
  test.skip(!panelTestsEnabled(), 'Panel testleri kapalı (E2E_RUN_PANEL_TESTS=true)');
  test.use({ storageState: MANAGER_AUTH_FILE });

  test.beforeAll(async () => {
    skipUnlessMutationsEnv();
    test.skip(!panelTestsEnabled(), 'Panel testleri kapalı (E2E_RUN_PANEL_TESTS=true)');
    test.skip(!isManagerAuthReady(), 'global-setup OTP başarısız — manager.json yok');
    await ensureE2eBusinessHours(E2E_TENANT_ID);
  });

  test('Panel: düzenle (tarih + hizmet + personel) → DB', async ({ page }) => {
    test.setTimeout(180_000);
    assertDbWritable();
    const token = getN8nAuthToken();
    const alt = await resolveAlternateServiceAndStaff(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );
    test.skip(!alt, 'İkinci hizmet/personel yok');

    const phone = uniquePhone();
    const name = uniqueName('PanelEdit');
    const booking = await resolveE2eBookingContext(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: name,
        customerPhone: phone,
        businessPhone: E2E_INSTANCE,
        serviceID: E2E_SERVICE_ID,
        appUserID: E2E_STAFF_ID,
        startDate: booking.startIso,
      },
      token,
      E2E_TENANT_ID,
    );

    const newIso = shiftSlotIso(booking.startIso, 15);
    const baseUrl = process.env.E2E_WEB_UI_URL ?? 'https://localhost:7140';
    await page.goto(`${baseUrl}${PANEL_URL}`);

    const digits = phone.replace(/\D/g, '').slice(-10);
    const row = page.locator('tr').filter({ hasText: digits }).first();
    await expect(row).toBeVisible({ timeout: 30_000 });
    await row.locator('.btn-edit-appointment').click();
    await expect(page.locator('#editAppointmentModal')).toBeVisible();

    await page.locator('#editServiceId').selectOption(String(alt!.serviceId));
    await page.locator('#editAppUserIdSelect').selectOption(String(alt!.staffId));
    await page.locator('#editAppointmentDate').fill(isoToPanelDate(newIso));
    await page.locator('#editAppointmentTime').fill(isoToPanelTime(newIso));
    await page.locator('#editAppointmentModal button[type="submit"]').click();
    await expect(page.locator('#editAppointmentModal')).toBeHidden({ timeout: 45_000 });

    await assertAppointmentFields(appointmentId, {
      serviceId: alt!.serviceId,
      staffId: alt!.staffId,
      startIsoPrefix: newIso,
    });

    await deleteAppointmentAsN8n(appointmentId, token, E2E_TENANT_ID);
  });

  test('Panel: randevu sil → DB', async ({ page }) => {
    test.setTimeout(120_000);
    assertDbWritable();
    const token = getN8nAuthToken();
    const phone = uniquePhone();
    const booking = await resolveE2eBookingContext(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: uniqueName('PanelDel'),
        customerPhone: phone,
        businessPhone: E2E_INSTANCE,
        serviceID: E2E_SERVICE_ID,
        appUserID: E2E_STAFF_ID,
        startDate: booking.startIso,
      },
      token,
      E2E_TENANT_ID,
    );

    const baseUrl = process.env.E2E_WEB_UI_URL ?? 'https://localhost:7140';
    await page.goto(`${baseUrl}${PANEL_URL}`);
    const digits = phone.replace(/\D/g, '').slice(-10);
    const row = page.locator('tr').filter({ hasText: digits }).first();
    await expect(row).toBeVisible({ timeout: 30_000 });

    await row.locator('.btn-delete-appointment').click();
    const confirm = page.locator('.swal2-confirm');
    await expect(confirm).toBeVisible({ timeout: 10_000 });
    await confirm.click();

    await expect
      .poll(() => appointmentExists(appointmentId), { timeout: 30_000, intervals: [1000, 2000] })
      .toBeFalsy();
  });
});

