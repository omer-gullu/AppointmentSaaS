/**
 * n8n + AI (Gemini) kenar durumları — Evolution API'ye doğrudan istek yok.
 *
 * Gerekli env:
 *   E2E_TENANT_ID
 *   E2E_INSTANCE_NAME
 *   E2E_N8N_TOKEN
 *   E2E_SERVICE_ID
 *   E2E_STAFF_ID
 *   E2E_CUSTOMER_PHONE_PREFIX (varsayılan 5320000)
 *   E2E_N8N_WEBHOOK_URL veya E2E_N8N_BASE_URL
 *
 * Çalıştırma (canlı n8n + Gemini gerekir):
 *   E2E_RUN_AI_EDGE=true npm run test -- tests/n8n-ai-edge-cases.spec.ts
 *   (veya E2E_RUN_GEMINI=true)
 */

import { test, expect } from '@playwright/test';
import {
  apiGetAsN8n,
  assertN8nWebhookOk,
  getN8nAuthToken,
  getN8nEvolutionWebhookUrl,
  resolveE2eBookingContext,
  sendN8nCustomerMessage,
  sleep,
} from '../helpers/n8n';
import {
  assertDbWritable,
  countAppointmentsForPhone,
  ensureE2eBreakTime,
  ensureE2eBusinessHours,
  getAppointmentById,
  requireDbConfigured,
  unblockCustomerPhone,
  waitForAppointment,
} from '../helpers/db';
import { createAppointmentAsN8n, deleteAppointmentAsN8n } from '../helpers/appointments';
import { isReadonlyEnv } from '../helpers/env';

const E2E_TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '0');
const E2E_INSTANCE = process.env.E2E_INSTANCE_NAME?.trim() ?? '';
const E2E_SERVICE_ID = Number(process.env.E2E_SERVICE_ID ?? '0');
const E2E_STAFF_ID = Number(process.env.E2E_STAFF_ID ?? '0');
const CUSTOMER_PREFIX = (process.env.E2E_CUSTOMER_PHONE_PREFIX ?? '5320000')
  .replace(/\D/g, '')
  .slice(0, 7);

const AI_WAIT_MS = Number(process.env.E2E_AI_EDGE_WAIT_MS ?? 15_000);
const AI_POLL_MS = Number(process.env.E2E_AI_EDGE_POLL_MS ?? 120_000);

let phoneSeq = 0;
const createdAppointmentIds: number[] = [];
const usedPhones: string[] = [];

test.describe.configure({ mode: 'serial' });

function pad2(n: number): string {
  return String(n).padStart(2, '0');
}

function localYmd(d: Date): string {
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}

function uniquePhone(): string {
  phoneSeq += 1;
  const suffix = String((Date.now() + phoneSeq * 13) % 1000).padStart(3, '0');
  return `${CUSTOMER_PREFIX}${suffix}`;
}

function tomorrowYmd(): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return localYmd(d);
}

function isAiEdgeEnabled(): boolean {
  return process.env.E2E_RUN_AI_EDGE === 'true' || process.env.E2E_RUN_GEMINI === 'true';
}

function skipUnlessAiEdgeLive(): void {
  test.skip(isReadonlyEnv(), 'readonly ortam');
  test.skip(!isAiEdgeEnabled(), 'E2E_RUN_AI_EDGE=true veya E2E_RUN_GEMINI=true gerekir');
  test.skip(!getN8nEvolutionWebhookUrl(), 'E2E_N8N_WEBHOOK_URL veya E2E_N8N_BASE_URL gerekli');
  test.skip(!E2E_TENANT_ID || !E2E_INSTANCE, 'E2E_TENANT_ID ve E2E_INSTANCE_NAME gerekli');
  test.skip(!E2E_SERVICE_ID || !E2E_STAFF_ID, 'E2E_SERVICE_ID ve E2E_STAFF_ID gerekli');
  test.skip(!getN8nAuthToken(), 'E2E_N8N_TOKEN gerekli');
}

async function prepareTenantHours(): Promise<void> {
  await ensureE2eBusinessHours(E2E_TENANT_ID);
  await ensureE2eBreakTime(E2E_TENANT_ID, '12:00', '13:00', true);
}

async function sendAiMessage(phone: string, messageText: string, step: string): Promise<void> {
  usedPhones.push(phone);
  const res = await sendN8nCustomerMessage({
    instanceName: E2E_INSTANCE,
    customerPhone: phone,
    messageText,
    pushName: 'E2E AI Edge',
  });
  assertN8nWebhookOk(res, step);
}

/** Workflow patlamasın; 4xx kabul (nazik red vb.). */
async function sendAiMessageSoft(phone: string, messageText: string, step: string): Promise<void> {
  usedPhones.push(phone);
  const res = await sendN8nCustomerMessage({
    instanceName: E2E_INSTANCE,
    customerPhone: phone,
    messageText,
    pushName: 'E2E AI Edge',
  });
  expect(res.status, `${step}: n8n workflow 5xx`).toBeLessThan(500);
  expect(res.body, `${step}`).not.toContain('Error in workflow');
}

async function tryWaitForAppointment(
  phone: string,
  timeoutMs = AI_POLL_MS,
): Promise<Awaited<ReturnType<typeof waitForAppointment>> | null> {
  try {
    return await waitForAppointment(E2E_TENANT_ID, phone.slice(-8), timeoutMs);
  } catch {
    return null;
  }
}

async function assertNoNewAppointment(
  phone: string,
  beforeCount: number,
  reason: string,
): Promise<void> {
  await sleep(AI_WAIT_MS);
  const after = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
  expect(after, `${reason} — önce=${beforeCount}, sonra=${after}`).toBe(beforeCount);
}

async function trackAppointmentForPhone(phone: string): Promise<void> {
  const row = await tryWaitForAppointment(phone);
  if (row?.AppointmentID) createdAppointmentIds.push(row.AppointmentID);
}

async function fetchFirstUpcomingHolidayYmd(): Promise<string | null> {
  const token = getN8nAuthToken();
  const ctx = await apiGetAsN8n('/api/Tenants/GetContextByInstance', E2E_TENANT_ID, token, {
    instanceName: E2E_INSTANCE,
  });
  if (ctx.status !== 200) return null;
  const mega = ctx.json as {
    upcomingHolidays?: { date?: string; Date?: string }[];
    UpcomingHolidays?: { date?: string; Date?: string }[];
  };
  const list = mega.upcomingHolidays ?? mega.UpcomingHolidays ?? [];
  for (const h of list) {
    const raw = (h.date ?? h.Date ?? '').trim();
    if (raw.length >= 10) return raw.slice(0, 10);
  }
  return null;
}

async function cleanupTestArtifacts(): Promise<void> {
  const token = getN8nAuthToken();
  for (const id of [...createdAppointmentIds]) {
    try {
      await deleteAppointmentAsN8n(id, token, E2E_TENANT_ID);
    } catch {
      /* ignore */
    }
  }
  createdAppointmentIds.length = 0;
  for (const phone of [...new Set(usedPhones)]) {
    await unblockCustomerPhone(E2E_TENANT_ID, phone).catch(() => {});
  }
  usedPhones.length = 0;
}

test.describe('n8n AI kenar durumları @ai-edge @gemini @destructive', () => {
  test.beforeAll(async () => {
    skipUnlessAiEdgeLive();
    requireDbConfigured();
    assertDbWritable();
    await prepareTenantHours();
  });

  test.afterEach(async () => {
    await cleanupTestArtifacts();
  });

  // ─── GRUP 1: Belirsiz Zaman ─────────────────────────────────────────────
  test.describe('Belirsiz Zaman Mesajları', () => {
    test.beforeAll(async () => {
      await prepareTenantHours();
    });

    test('"yarın randevu istiyorum" → yarın tarihli kayıt', async () => {
      test.setTimeout(300_000);
      const phone = uniquePhone();
      await sendAiMessage(phone, 'yarın randevu istiyorum', 'yarın randevu');
      await sleep(AI_WAIT_MS);

      const row = await tryWaitForAppointment(phone);
      expect(row, 'DB\'de randevu kaydı bekleniyordu').not.toBeNull();
      if (!row) return;

      createdAppointmentIds.push(row.AppointmentID);
      const detail = await getAppointmentById(row.AppointmentID);
      expect(detail, 'Randevu detayı okunamadı').not.toBeNull();
      expect(localYmd(detail!.StartDate), 'Tarih yarın olmalı').toBe(tomorrowYmd());
    });

    test('"öğleden sonra randevu" → 12:00–17:00 arası', async () => {
      test.setTimeout(300_000);
      const phone = uniquePhone();
      await sendAiMessage(phone, 'öğleden sonra randevu', 'öğleden sonra');
      await sleep(AI_WAIT_MS);

      const row = await tryWaitForAppointment(phone);
      expect(row, 'Öğleden sonra randevu DB kaydı bekleniyordu').not.toBeNull();
      if (!row) return;

      createdAppointmentIds.push(row.AppointmentID);
      const detail = await getAppointmentById(row.AppointmentID);
      const hour = new Date(detail!.StartDate).getHours();
      expect(hour, `Saat öğleden sonra olmalı (12–16), alınan: ${hour}`).toBeGreaterThanOrEqual(12);
      expect(hour).toBeLessThan(17);
    });

    test('"bu hafta sonu müsait misiniz" → yeni randevu açmamalı', async () => {
      test.setTimeout(180_000);
      const phone = uniquePhone();
      const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      await sendAiMessage(phone, 'bu hafta sonu müsait misiniz', 'hafta sonu bilgi');
      await assertNoNewAppointment(phone, before, 'Hafta sonu bilgi sorusu randevu açmamalı');
    });
  });

  // ─── GRUP 2: Niyet Belirsizliği ─────────────────────────────────────────
  test.describe('Niyet Belirsizliği', () => {
    test.beforeAll(async () => {
      await prepareTenantHours();
    });

    test('"merhaba" → randevu açmamalı', async () => {
      test.setTimeout(120_000);
      const phone = uniquePhone();
      const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      await sendAiMessage(phone, 'merhaba', 'merhaba');
      await assertNoNewAppointment(phone, before, 'Selamlama randevu açmamalı');
    });

    test('"iptal etmek istiyorum" (kayıt yok) → crash yok', async () => {
      test.setTimeout(120_000);
      const phone = uniquePhone();
      const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      await sendAiMessageSoft(phone, 'iptal etmek istiyorum', 'iptal yokken');
      await assertNoNewAppointment(phone, before, 'Kayıt yokken iptal yeni randevu açmamalı');
    });

    test('"müsait misiniz" → randevu açmamalı', async () => {
      test.setTimeout(120_000);
      const phone = uniquePhone();
      const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      await sendAiMessage(phone, 'müsait misiniz', 'müsait misiniz');
      await assertNoNewAppointment(phone, before, 'Müsaitlik sorusu randevu açmamalı');
    });
  });

  // ─── GRUP 3: Çakışma ────────────────────────────────────────────────────
  test.describe('Çakışma Senaryoları', () => {
    test.beforeAll(async () => {
      await prepareTenantHours();
    });

    test('dolu saate ikinci randevu → çakışma / tek kayıt', async () => {
      test.setTimeout(300_000);
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
          customerName: 'E2E Slot Dolu',
          customerPhone: phone,
          businessPhone: E2E_INSTANCE,
          serviceID: E2E_SERVICE_ID,
          appUserID: E2E_STAFF_ID,
          startDate: booking.startIso,
        },
        token,
        E2E_TENANT_ID,
      );
      createdAppointmentIds.push(appointmentId);

      await sendAiMessage(
        phone,
        `${booking.slotDate} günü saat ${booking.slotTime} için randevu almak istiyorum`,
        'dolu slota tekrar',
      );
      await sleep(AI_WAIT_MS);

      const total = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      expect(
        total,
        'Aynı slotta en fazla bir randevu olmalı (çakışan ikinci kayıt yok)',
      ).toBeLessThanOrEqual(1);
    });

    test('gece 02:00 randevu → açılmamalı veya iş saati içi', async () => {
      test.setTimeout(300_000);
      const phone = uniquePhone();
      const target = tomorrowYmd();
      await sendAiMessage(
        phone,
        `${target} tarihinde gece saat 02:00 için randevu istiyorum`,
        'gece 02:00',
      );
      await sleep(AI_WAIT_MS);

      const row = await tryWaitForAppointment(phone, 30_000);
      if (!row) return;

      createdAppointmentIds.push(row.AppointmentID);
      const detail = await getAppointmentById(row.AppointmentID);
      const hour = new Date(detail!.StartDate).getHours();
      expect(
        hour >= 8 && hour < 21,
        `Gece 02:00 isteği iş saati dışına düşmemeli; saat=${hour}`,
      ).toBeTruthy();
    });

    test('tatil gününe randevu → açılmamalı', async () => {
      test.setTimeout(300_000);
      const holidayYmd = await fetchFirstUpcomingHolidayYmd();
      test.skip(!holidayYmd, 'Tenant için upcoming holiday yok — test atlandı');

      const phone = uniquePhone();
      const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      await sendAiMessage(
        phone,
        `${holidayYmd} tarihine randevu almak istiyorum`,
        'tatil günü',
      );
      await sleep(AI_WAIT_MS);

      const row = await tryWaitForAppointment(phone, 25_000);
      if (row?.AppointmentID) {
        const detail = await getAppointmentById(row.AppointmentID);
        const apptYmd = detail ? localYmd(detail.StartDate) : '';
        expect(
          apptYmd,
          `Tatil günü (${holidayYmd}) için randevu oluşmamalı, oluştu: ${apptYmd}`,
        ).not.toBe(holidayYmd);
        createdAppointmentIds.push(row.AppointmentID);
      } else {
        await assertNoNewAppointment(phone, before, 'Tatil günü randevu açmamalı');
      }
    });
  });

  // ─── GRUP 4: Spam / Anlamsız ────────────────────────────────────────────
  test.describe('Spam / Anlamsız Mesajlar', () => {
    test.beforeAll(async () => {
      await prepareTenantHours();
    });

    test('anlamsız mesaj "asdfghjkl"', async () => {
      test.setTimeout(120_000);
      const phone = uniquePhone();
      const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      await sendAiMessageSoft(phone, 'asdfghjkl', 'anlamsız');
      await assertNoNewAppointment(phone, before, 'Anlamsız mesaj randevu açmamalı');
    });

    test('500+ karakter mesaj', async () => {
      test.setTimeout(120_000);
      const phone = uniquePhone();
      const longText = `${'lorem ipsum '.repeat(60)}randevu`;
      expect(longText.length).toBeGreaterThan(500);
      const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      await sendAiMessageSoft(phone, longText, 'uzun mesaj');
      await assertNoNewAppointment(phone, before, 'Uzun mesaj randevu açmamalı');
    });

    test('emoji mesaj', async () => {
      test.setTimeout(120_000);
      const phone = uniquePhone();
      const before = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      await sendAiMessageSoft(phone, '🎉🎊 randevu 📅', 'emoji');
      await assertNoNewAppointment(phone, before, 'Emoji mesajı graceful işlenmeli');
    });
  });

  // ─── GRUP 5: Art Arda / Paralel ─────────────────────────────────────────
  test.describe('Art Arda Mesajlar (Aynı Numara)', () => {
    test.beforeAll(async () => {
      await prepareTenantHours();
    });

    test('3 sn arayla "yarın randevu" sonra "iptal"', async () => {
      test.setTimeout(360_000);
      const phone = uniquePhone();
      await sendAiMessage(phone, 'yarın randevu', 'ardışık 1');
      await sleep(AI_WAIT_MS);
      await trackAppointmentForPhone(phone);

      await sleep(3_000);
      await sendAiMessageSoft(phone, 'iptal', 'ardışık 2 iptal');
      await sleep(AI_WAIT_MS);

      const count = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      expect(count, 'İptal sonrası duplicate randevu olmamalı').toBeLessThanOrEqual(1);
    });

    test('aynı anda 3 paralel mesaj → crash yok, duplicate yok', async () => {
      test.setTimeout(300_000);
      const phone = uniquePhone();
      usedPhones.push(phone);
      const texts = ['merhaba', 'yarın randevu', 'müsait misiniz'];

      const results = await Promise.all(
        texts.map((messageText) =>
          sendN8nCustomerMessage({
            instanceName: E2E_INSTANCE,
            customerPhone: phone,
            messageText,
            pushName: 'E2E Parallel',
          }),
        ),
      );

      for (let i = 0; i < results.length; i++) {
        expect(results[i].status, `paralel mesaj ${i + 1}`).toBeLessThan(500);
        expect(results[i].body).not.toContain('Error in workflow');
      }

      await sleep(AI_WAIT_MS);
      const count = await countAppointmentsForPhone(E2E_TENANT_ID, phone);
      expect(count, 'Paralel flood sonrası en fazla 1 randevu').toBeLessThanOrEqual(1);
      await trackAppointmentForPhone(phone);
    });
  });
});
