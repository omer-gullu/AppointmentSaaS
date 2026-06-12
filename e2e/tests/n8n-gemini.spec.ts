import { test, expect } from '@playwright/test';
import {
  apiGetAsN8n,
  assertN8nWebhookOk,
  formatWhatsAppJid,
  geminiMessageDelayMs,
  getN8nAuthToken,
  getN8nEvolutionWebhookUrl,
  isGeminiE2eEnabled,
  resolveE2eBookingContext,
  sendN8nCustomerMessage,
  sleep,
} from '../helpers/n8n';
import { assertDbWritable, ensureE2eBusinessHours, requireDbConfigured, waitForAppointment } from '../helpers/db';
import { isReadonlyEnv } from '../helpers/env';

const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const E2E_INSTANCE = process.env.E2E_INSTANCE_NAME?.trim();
const E2E_SERVICE_ID = Number(process.env.E2E_SERVICE_ID ?? '0');
const E2E_STAFF_ID = Number(process.env.E2E_STAFF_ID ?? '0');

const CUSTOMER_PREFIX = (process.env.E2E_CUSTOMER_PHONE_PREFIX ?? '5320000').replace(/\D/g, '').slice(0, 7);

test.describe.configure({ mode: 'serial' });

/**
 * Ücretli: Gemini (n8n AI Agent) → kendi_sistemine_kaydet → DB.
 * Çalıştırmak için: E2E_RUN_GEMINI=true npm run test:n8n:gemini
 */
test.describe('n8n + Gemini randevu @gemini @destructive', () => {
  const customerPhone = `${CUSTOMER_PREFIX}${String(Date.now() % 1000).padStart(3, '0')}`;
  const customerName = 'Esse Gemini Etwo';

  test.beforeAll(async () => {
    const n8nUrl = getN8nEvolutionWebhookUrl();
    test.skip(isReadonlyEnv(), 'readonly ortam');
    test.skip(!isGeminiE2eEnabled(), 'E2E_RUN_GEMINI=true ile çalıştırın (Gemini ücretli)');
    test.skip(
      !n8nUrl,
      'E2E_N8N_WEBHOOK_URL veya E2E_N8N_BASE_URL .env içinde tanımlı değil (şu an atlandı)',
    );
    test.skip(!E2E_TENANT_ID || !E2E_INSTANCE, 'E2E_TENANT_ID ve E2E_INSTANCE_NAME gerekli');
    test.skip(!E2E_SERVICE_ID || !E2E_STAFF_ID, 'E2E_SERVICE_ID ve E2E_STAFF_ID gerekli');
    test.skip(!getN8nAuthToken(), 'E2E_N8N_TOKEN gerekli');

    requireDbConfigured();
    assertDbWritable();
    await ensureE2eBusinessHours(E2E_TENANT_ID);

    const token = getN8nAuthToken();
    const check = await apiGetAsN8n('/api/WhatsAppBlockedPhones/check', E2E_TENANT_ID, token, {
      phone: formatWhatsAppJid(customerPhone),
      tenantId: String(E2E_TENANT_ID),
      instanceName: E2E_INSTANCE!,
    });
    if (check.status === 200 && (check.json as { blocked?: boolean }).blocked) {
      throw new Error(
        `Test telefonu gri listede (${customerPhone}). Başka numara veya listeden çıkarın.`,
      );
    }

    const ctx = await apiGetAsN8n('/api/Tenants/GetContextByInstance', E2E_TENANT_ID, token, {
      instanceName: E2E_INSTANCE!,
    });
    if (ctx.status !== 200) {
      throw new Error(
        `API GetContextByInstance başarısız (${ctx.status}). n8n'den de aynı URL kullanılıyor — önce API'yi düzeltin.\n${JSON.stringify(ctx.json)}`,
      );
    }
  });

  test('WhatsApp mesajı → Gemini → randevu DB kaydı', async () => {
    test.setTimeout(600_000);

    const token = getN8nAuthToken();
    const booking = await resolveE2eBookingContext(
      E2E_TENANT_ID,
      E2E_INSTANCE!,
      token,
      E2E_SERVICE_ID,
      E2E_STAFF_ID,
    );

    const delay = geminiMessageDelayMs();

    const intro = await sendN8nCustomerMessage({
      instanceName: E2E_INSTANCE!,
      customerPhone,
      messageText: 'Merhaba, randevu almak istiyorum.',
      pushName: customerName,
    });
    assertN8nWebhookOk(intro, '1. mesaj (Merhaba)');
    await sleep(delay);

    const details = await sendN8nCustomerMessage({
      instanceName: E2E_INSTANCE!,
      customerPhone,
      messageText:
        `Adım ${customerName}. Telefonum ${customerPhone}. ` +
        `Hizmet: ${booking.serviceName} (ServiceID ${E2E_SERVICE_ID}). ` +
        `Personel: ${booking.staffName} (AppUserID ${E2E_STAFF_ID}). ` +
        `Uygun saatlerden ${booking.slotDate} günü ${booking.slotTime} istiyorum.`,
      pushName: customerName,
    });
    assertN8nWebhookOk(details, '2. mesaj (detay)');
    await sleep(delay);

    const confirm = await sendN8nCustomerMessage({
      instanceName: E2E_INSTANCE!,
      customerPhone,
      messageText:
        `Evet, onaylıyorum. Lütfen randevuyu kaydet. ` +
        `StartDate: ${booking.startIso} (YYYY-MM-DDTHH:mm:ss). ` +
        `kendi_sistemine_kaydet aracını kullan.`,
      pushName: customerName,
    });
    expect(confirm.status, confirm.body.slice(0, 300)).toBeLessThan(500);

    const pollTimeout = Number(process.env.E2E_GEMINI_DB_POLL_TIMEOUT_MS ?? 360_000);

    await expect
      .poll(
        async () => {
          try {
            const row = await waitForAppointment(E2E_TENANT_ID, customerPhone.slice(-8), 5_000);
            return row.CustomerName?.toLowerCase().includes('esse') ?? false;
          } catch {
            return false;
          }
        },
        { timeout: pollTimeout, intervals: [5000, 8000, 12000, 15000] },
      )
      .toBeTruthy();
  });
});
