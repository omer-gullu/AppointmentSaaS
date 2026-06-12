import { test, expect } from '@playwright/test';
import {
  apiDeleteAsN8n,
  apiGetAsN8n,
  apiPutAsN8n,
  formatWhatsAppJid,
  getN8nSystemAuthToken,
  resolveE2eBookingContext,
} from '../helpers/n8n';
import {
  createAppointmentAsN8n,
  deleteAppointmentAsN8n,
  shiftSlotIso,
} from '../helpers/appointments';
import { requireE2eStaticConfig, type E2eStaticConfig } from '../helpers/e2e-config';
import { appointmentExists, ensureE2eBusinessHours, requireDbConfigured } from '../helpers/db';

let cfg: E2eStaticConfig;
let n8nToken = '';
let e2eInstance = '';

/**
 * n8n workflow içindeki HTTP Request node'larının vurduğu API uçları.
 * n8n sunucusu çalışmasa da "sözleşme" doğrulanır (Gemini'den bağımsız).
 */
test.describe('n8n API sözleşmesi', () => {
  test.beforeAll(async () => {
    requireDbConfigured();
    cfg = requireE2eStaticConfig();
    n8nToken = cfg.n8nToken;
    e2eInstance = cfg.instanceName;
    const probe = await apiGetAsN8n('/api/Tenants/GetContextByInstance', cfg.tenantId, n8nToken, {
      instanceName: e2eInstance,
    });
    if (probe.status === 401) {
      throw new Error(
        `E2E_N8N_TOKEN / instance uyuşmuyor (tenant=${cfg.tenantId}, instance=${e2eInstance}). discover-env.ps1 ile .env güncelleyin.`,
      );
    }
    await ensureE2eBusinessHours(cfg.tenantId);
  });

  test('GetContextByInstance → işletme bağlamı', async () => {
    const token = n8nToken;
    const res = await apiGetAsN8n('/api/Tenants/GetContextByInstance', cfg.tenantId, token, {
      instanceName: e2eInstance,
    });
    expect(res.status, JSON.stringify(res.json)).toBe(200);
    const body = res.json as Record<string, unknown>;
    const tid = body.tenantId ?? body.TenantID ?? body.tenantID;
    expect(Number(tid)).toBe(cfg.tenantId);
    expect(body.services ?? body.Services).toBeTruthy();
  });

  test('available-slots → müsait saat listesi veya tatil', async () => {
    const token = n8nToken;
    const d = new Date();
    d.setDate(d.getDate() + 21);
    while (d.getDay() === 0) d.setDate(d.getDate() + 1);
    const date = d.toISOString().slice(0, 10);

    const res = await apiGetAsN8n('/api/Appointments/available-slots', cfg.tenantId, token, {
      instanceName: e2eInstance,
      staffId: String(cfg.staffId),
      date,
      durationMinutes: '30',
    });
    expect(res.status, JSON.stringify(res.json)).toBe(200);
    const body = res.json as Record<string, unknown>;
    if (body.isHoliday === true) {
      const slots = body.availableSlots ?? body.AvailableSlots;
      expect(slots).toEqual([]);
    } else {
      expect(
        Array.isArray(body.availableSlots) ||
          Array.isArray(body.AvailableSlots) ||
          body.totalSlots != null ||
          body.TotalSlots != null,
      ).toBeTruthy();
    }
  });

  test('WhatsAppBlockedPhones/check → gri liste sorgusu', async () => {
    const token = n8nToken;
    const phone = '905320000199';
    const res = await apiGetAsN8n('/api/WhatsAppBlockedPhones/check', cfg.tenantId, token, {
      phone,
      tenantId: String(cfg.tenantId),
      instanceName: e2eInstance,
    });
    expect(res.status, JSON.stringify(res.json)).toBe(200);
    const body = res.json as { blocked?: boolean };
    expect(typeof body.blocked).toBe('boolean');
  });

  test('reminders/pending → hatırlatma kuyruğu (Pro/Business)', async () => {
    const token = getN8nSystemAuthToken();
    test.skip(!token, 'E2E_N8N_SYSTEM_TOKEN veya API WebhookSecurity:N8nAuthToken gerekli');
    const res = await apiGetAsN8n('/api/Appointments/reminders/pending', cfg.tenantId, token);
    expect(res.status, JSON.stringify(res.json)).toBe(200);
    expect(Array.isArray(res.json)).toBe(true);
  });

  test('PUT /api/Appointments/{id} → güncelle (randevu_güncelle aracı)', async () => {
    const token = n8nToken;
    const phone = `5320000${String(Date.now() % 1000).padStart(3, '0')}`;
    const booking = await resolveE2eBookingContext(
      cfg.tenantId,
      e2eInstance,
      token,
      cfg.serviceId,
      cfg.staffId,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: 'Esse Contract Upd',
        customerPhone: phone,
        businessPhone: e2eInstance,
        serviceID: cfg.serviceId,
        appUserID: cfg.staffId,
        startDate: booking.startIso,
      },
      token,
      cfg.tenantId,
    );

    const newIso = shiftSlotIso(booking.startIso, 5);
    const put = await apiPutAsN8n(`/api/Appointments/${appointmentId}`, cfg.tenantId, token, {
      customerName: 'Esse Contract Upd',
      customerPhone: phone,
      serviceID: cfg.serviceId,
      appUserID: cfg.staffId,
      startDate: newIso,
      businessPhone: e2eInstance,
    });
    expect(put.status, JSON.stringify(put.json)).toBe(200);

    await deleteAppointmentAsN8n(appointmentId, token, cfg.tenantId);
  });

  test('DELETE /api/Appointments/{id} → sil (randevu_sil aracı)', async () => {
    const token = n8nToken;
    const phone = `5320000${String((Date.now() + 1) % 1000).padStart(3, '0')}`;
    const booking = await resolveE2eBookingContext(
      cfg.tenantId,
      e2eInstance,
      token,
      cfg.serviceId,
      cfg.staffId,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: 'Esse Contract Del',
        customerPhone: phone,
        businessPhone: e2eInstance,
        serviceID: cfg.serviceId,
        appUserID: cfg.staffId,
        startDate: booking.startIso,
      },
      token,
      cfg.tenantId,
    );

    const del = await apiDeleteAsN8n(`/api/Appointments/${appointmentId}`, cfg.tenantId, token);
    expect(del.status, JSON.stringify(del.json)).toBe(200);
    expect(await appointmentExists(appointmentId)).toBeFalsy();
  });

  test('my-active-appointments → randevulari_oku aracı', async () => {
    const token = n8nToken;
    const phone = `5320000${String((Date.now() + 2) % 1000).padStart(3, '0')}`;
    const jid = formatWhatsAppJid(phone);
    const res = await apiGetAsN8n('/api/Appointments/my-active-appointments', cfg.tenantId, token, {
      instanceName: e2eInstance,
      phone: jid,
    });
    expect(res.status, JSON.stringify(res.json)).toBe(200);
    const body = res.json as { hasActiveAppointment?: boolean };
    expect(typeof body.hasActiveAppointment).toBe('boolean');
  });
});
