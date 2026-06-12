import { test, expect } from '@playwright/test';
import {
  apiGetAsN8n,
  assertN8nWebhookOk,
  buildEvolutionWebhookPayload,
  formatWhatsAppJid,
  getN8nAuthToken,
  getN8nEvolutionWebhookUrl,
  isPhoneBlocked,
  postEvolutionWebhookToN8n,
  sendN8nCustomerMessage,
  waitForPhoneBlocked,
} from '../helpers/n8n';
import {
  countAppointmentsForPhone,
  requireDbConfigured,
  unblockCustomerPhone,
} from '../helpers/db';
import { createAppointmentAsN8n, deleteAppointmentAsN8n } from '../helpers/appointments';
import { resolveE2eBookingContext } from '../helpers/n8n';
import { isReadonlyEnv } from '../helpers/env';

const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const E2E_INSTANCE = process.env.E2E_INSTANCE_NAME?.trim() ?? '';
const E2E_SERVICE_ID = Number(process.env.E2E_SERVICE_ID ?? '0');
const E2E_STAFF_ID = Number(process.env.E2E_STAFF_ID ?? '0');
const CUSTOMER_PREFIX = (process.env.E2E_CUSTOMER_PHONE_PREFIX ?? '5320000').replace(/\D/g, '').slice(0, 7);

function sandboxPhone(): string {
  return `${CUSTOMER_PREFIX}${String(Date.now() % 1000).padStart(3, '0')}`;
}

test.describe.configure({ mode: 'serial' });

function skipUnlessN8nLive(): void {
  test.skip(isReadonlyEnv(), 'readonly ortam');
  test.skip(!getN8nEvolutionWebhookUrl(), 'E2E_N8N_WEBHOOK_URL veya E2E_N8N_BASE_URL gerekli');
  test.skip(!E2E_TENANT_ID || !E2E_INSTANCE, 'E2E_TENANT_ID ve E2E_INSTANCE_NAME gerekli');
  test.skip(!getN8nAuthToken(), 'E2E_N8N_TOKEN gerekli');
}

/**
 * Canlı n8n workflow davranışları (Gemini ücreti yok — çoğu dal).
 * Özür / 2. agent: E2E_N8N_VERIFY_AI_FAILURE=true (manuel test modu) gerekir.
 */
test.describe('n8n workflow davranış @destructive', () => {
  test.beforeAll(() => {
    skipUnlessN8nLive();
    requireDbConfigured();
  });

  test.afterEach(async () => {
    if (!E2E_TENANT_ID) return;
    // test telefonlarını gri listeden temizle
  });

  test('"asistanı kapat" (küçük) → gri liste', async () => {
    const phone = sandboxPhone();
    const token = getN8nAuthToken();
    const payload = buildEvolutionWebhookPayload({
      instanceName: E2E_INSTANCE,
      customerPhone: phone,
      messageText: 'asistanı kapat',
      pushName: 'E2E OptOut Lower',
    });
    const res = await postEvolutionWebhookToN8n(payload);
    assertN8nWebhookOk(res, 'asistanı kapat');
    await waitForPhoneBlocked(E2E_TENANT_ID, token, phone, E2E_INSTANCE);
    await unblockCustomerPhone(E2E_TENANT_ID, phone);
  });

  test('"Asistanı kapat" (büyük A) → gri liste', async () => {
    const phone = sandboxPhone();
    const token = getN8nAuthToken();
    const payload = buildEvolutionWebhookPayload({
      instanceName: E2E_INSTANCE,
      customerPhone: phone,
      messageText: 'Asistanı kapat',
      pushName: 'E2E OptOut Title',
    });
    const res = await postEvolutionWebhookToN8n(payload);
    assertN8nWebhookOk(res, 'Asistanı kapat');
    await waitForPhoneBlocked(E2E_TENANT_ID, token, phone, E2E_INSTANCE);
    await unblockCustomerPhone(E2E_TENANT_ID, phone);
  });

  test('gri listedeki numaraya mesaj → yeni randevu oluşmaz (AI yanıt proxy)', async () => {
    test.setTimeout(120_000);
    const phone = sandboxPhone();
    const token = getN8nAuthToken();
    const jid = formatWhatsAppJid(phone);

    const optOut = buildEvolutionWebhookPayload({
      instanceName: E2E_INSTANCE,
      customerPhone: phone,
      messageText: 'asistanı kapat',
    });
    assertN8nWebhookOk(await postEvolutionWebhookToN8n(optOut), 'opt-out');

    await waitForPhoneBlocked(E2E_TENANT_ID, token, phone, E2E_INSTANCE);
    expect(await isPhoneBlocked(E2E_TENANT_ID, token, jid, E2E_INSTANCE)).toBeTruthy();

    const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
    const msg = buildEvolutionWebhookPayload({
      instanceName: E2E_INSTANCE,
      customerPhone: phone,
      messageText: 'Merhaba randevu almak istiyorum yarın saat 10',
      pushName: 'E2E Blocked',
    });
    const res = await postEvolutionWebhookToN8n(msg);
    expect(res.status, res.body.slice(0, 300)).toBeLessThan(500);

    await new Promise((r) => setTimeout(r, 15_000));
    const after = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
    expect(after).toBe(before);

    await unblockCustomerPhone(E2E_TENANT_ID, phone);
  });

  test('Redis rate limit: ardışık mesajlar workflow patlatmaz', async () => {
    test.setTimeout(180_000);
    const phone = sandboxPhone();
    const burst = Number(process.env.E2E_RATE_LIMIT_BURST ?? 16);
    const statuses: number[] = [];

    for (let i = 0; i < burst; i++) {
      const payload = buildEvolutionWebhookPayload({
        instanceName: E2E_INSTANCE,
        customerPhone: phone,
        messageText: `rate test ${i}`,
        pushName: 'E2E Rate',
      });
      const res = await postEvolutionWebhookToN8n(payload, 60_000);
      statuses.push(res.status);
    }

    const errors = statuses.filter((s) => s >= 500);
    expect(errors, `500 sayısı: ${errors.length}, tüm status: ${statuses.join(',')}`).toHaveLength(0);
    await unblockCustomerPhone(E2E_TENANT_ID, phone);
  });

  test('webhook sonrası GetContext (Hafızamı tazele proxy) → megaContext dolu', async () => {
    test.setTimeout(120_000);
    const phone = sandboxPhone();
    const token = getN8nAuthToken();

    const res = await sendN8nCustomerMessage({
      instanceName: E2E_INSTANCE,
      customerPhone: phone,
      messageText: 'Merhaba',
      pushName: 'E2E Context',
    });
    assertN8nWebhookOk(res, 'merhaba');

    await expect
      .poll(
        async () => {
          const ctx = await apiGetAsN8n('/api/Tenants/GetContextByInstance', E2E_TENANT_ID, token, {
            instanceName: E2E_INSTANCE,
          });
          if (ctx.status !== 200) return false;
          const body = ctx.json as Record<string, unknown>;
          const services = body.services ?? body.Services;
          const staffs = body.staffs ?? body.Staffs;
          return Array.isArray(services) && services.length > 0 && Array.isArray(staffs) && staffs.length > 0;
        },
        { timeout: 60_000, intervals: [2000, 4000, 6000] },
      )
      .toBeTruthy();

    await unblockCustomerPhone(E2E_TENANT_ID, phone);
  });

  test('randevu oluştur → my-active-appointments (randevulari_oku proxy)', async () => {
    const token = getN8nAuthToken();
    const phone = sandboxPhone();
    const booking = await resolveE2eBookingContext(
      E2E_TENANT_ID,
      E2E_INSTANCE,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: 'Esse Active List',
        customerPhone: phone,
        businessPhone: E2E_INSTANCE,
        serviceID: E2E_SERVICE_ID,
        appUserID: E2E_STAFF_ID,
        startDate: booking.startIso,
      },
      token,
      E2E_TENANT_ID,
    );

    const jid = formatWhatsAppJid(phone);
    const active = await apiGetAsN8n('/api/Appointments/my-active-appointments', E2E_TENANT_ID, token, {
      instanceName: E2E_INSTANCE,
      remoteJid: jid,
    });
    expect(active.status, JSON.stringify(active.json)).toBe(200);
    const body = active.json as { hasActiveAppointment?: boolean; appointments?: unknown[] };
    expect(body.hasActiveAppointment).toBeTruthy();
    expect((body.appointments ?? []).length).toBeGreaterThan(0);

    await deleteAppointmentAsN8n(appointmentId, token, E2E_TENANT_ID);
  });

  test('1. agent hata → 2. agent (manuel doğrulama)', async () => {
    test.skip(
      process.env.E2E_N8N_VERIFY_AI_FAILURE !== 'true',
      'Vertex/Gemini failover için n8n test modunda 1. agentı bilerek hata verdirin; E2E_N8N_VERIFY_AI_FAILURE=true',
    );
    test.setTimeout(300_000);
    const phone = sandboxPhone();
    const res = await sendN8nCustomerMessage({
      instanceName: E2E_INSTANCE,
      customerPhone: phone,
      messageText: process.env.E2E_N8N_FAILURE_TRIGGER_MESSAGE ?? 'E2E_FORCE_AGENT_FAIL',
      pushName: 'E2E Failover',
    });
    assertN8nWebhookOk(res, 'failover trigger');
    await unblockCustomerPhone(E2E_TENANT_ID, phone);
  });

  test('özür mesajı dalı (manuel doğrulama)', async () => {
    test.skip(
      process.env.E2E_N8N_VERIFY_AI_FAILURE !== 'true',
      'Her iki agent da hata verince özür dalı — n8n test modu + E2E_N8N_VERIFY_AI_FAILURE=true',
    );
    test.setTimeout(300_000);
    const phone = sandboxPhone();
    const res = await sendN8nCustomerMessage({
      instanceName: E2E_INSTANCE,
      customerPhone: phone,
      messageText: process.env.E2E_N8N_FAILURE_TRIGGER_MESSAGE ?? 'E2E_FORCE_AGENT_FAIL',
      pushName: 'E2E Apology',
    });
    assertN8nWebhookOk(res, 'apology branch');
    // Evolution gönderimini E2E doğrulayamaz; n8n Executions + Evolution log kontrol edin.
    await unblockCustomerPhone(E2E_TENANT_ID, phone);
  });
});
