import { test, expect } from '@playwright/test';
import {
  apiGetAsN8n,
  buildEvolutionWebhookPayload,
  formatWhatsAppJid,
  getN8nAuthToken,
  getN8nEvolutionWebhookUrl,
  postEvolutionWebhookToN8n,
} from '../helpers/n8n';
import { isReadonlyEnv } from '../helpers/env';

const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const E2E_INSTANCE = process.env.E2E_INSTANCE_NAME?.trim();

test.describe.configure({ mode: 'serial' });

/**
 * Gerçek n8n instance: Evolution webhook URL'ine POST.
 * "asistanı kapat" dalı AI (Gemini) gerektirmez — gri listeye ekleme API'sini doğrular.
 *
 * Gerekli: n8n workflow Active + E2E_N8N_WEBHOOK_URL veya E2E_N8N_BASE_URL
 * Örnek: E2E_N8N_BASE_URL=http://localhost:5678
 */
test.describe('n8n canlı workflow @destructive', () => {
  const sandboxPhone = `5320000${String(Date.now() % 1000).padStart(3, '0')}`;

  test.beforeAll(() => {
    test.skip(isReadonlyEnv(), 'readonly ortam');
    test.skip(!getN8nEvolutionWebhookUrl(), 'E2E_N8N_WEBHOOK_URL veya E2E_N8N_BASE_URL gerekli');
    test.skip(!E2E_TENANT_ID || !E2E_INSTANCE, 'E2E_TENANT_ID ve E2E_INSTANCE_NAME gerekli');
    test.skip(!getN8nAuthToken(), 'E2E_N8N_TOKEN gerekli');
  });

  test('"asistanı kapat" → n8n webhook + gri liste (Gemini olmadan)', async () => {
    test.setTimeout(180_000);
    const token = getN8nAuthToken();
    const jid = formatWhatsAppJid(sandboxPhone);

    const payload = buildEvolutionWebhookPayload({
      instanceName: E2E_INSTANCE!,
      customerPhone: sandboxPhone,
      messageText: 'asistanı kapat',
      pushName: 'E2E OptOut',
    });

    const { status, body } = await postEvolutionWebhookToN8n(payload);
    expect(status, body.slice(0, 500)).toBeGreaterThanOrEqual(200);
    expect(status).toBeLessThan(500);

    await expect
      .poll(
        async () => {
          const check = await apiGetAsN8n('/api/WhatsAppBlockedPhones/check', E2E_TENANT_ID, token, {
            phone: jid,
            tenantId: String(E2E_TENANT_ID),
            instanceName: E2E_INSTANCE!,
          });
          if (check.status !== 200) return false;
          return (check.json as { blocked?: boolean }).blocked === true;
        },
        { timeout: 90_000, intervals: [2000, 3000, 5000] },
      )
      .toBeTruthy();
  });
});
