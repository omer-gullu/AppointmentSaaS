import { test, expect } from '@playwright/test';
import {
  activeAppointmentIds,
  apiDeleteAsN8n,
  apiGetAsN8n,
  apiPutAsN8n,
  canPlanUseReminders,
  fetchMyActiveAppointments,
  formatWhatsAppJid,
  getN8nSystemAuthToken,
  istanbulYmdPlusDays,
  resolveE2eBookingContext,
  weekdayOfYmd,
} from '../helpers/n8n';
import { createAppointmentAsN8n, deleteAppointmentAsN8n } from '../helpers/appointments';
import { requireE2eStaticConfig, type E2eStaticConfig } from '../helpers/e2e-config';
import { resolveStaffIdWithGoogleToken, skipUnlessGoogleStaff } from '../helpers/google-e2e';

let cfg: E2eStaticConfig;
let n8nToken = '';
let e2eInstance = '';
let planType = '';
let googleStaffId: number | null = null;
let preferredStaffId = 0;

type SlotBody = {
  isHoliday?: boolean;
  availableSlots?: string[];
  AvailableSlots?: string[];
  totalSlots?: number;
  TotalSlots?: number;
};

function slotTimes(body: SlotBody): string[] {
  const raw = body.availableSlots ?? body.AvailableSlots ?? [];
  return raw.map((t) => String(t).trim()).filter(Boolean);
}

async function availableSlotsFor(date: string): Promise<{ status: number; body: SlotBody }> {
  const res = await apiGetAsN8n('/api/Appointments/available-slots', cfg.tenantId, n8nToken, {
    instanceName: e2eInstance,
    staffId: String(cfg.staffId),
    date,
    durationMinutes: '30',
  });
  return { status: res.status, body: (res.json ?? {}) as SlotBody };
}

/** n8n workflow içindeki HTTP Request node'larının vurduğu canlı API uçları. */
test.describe('n8n API sözleşmesi', () => {
  test.beforeAll(async () => {
    cfg = requireE2eStaticConfig();
    n8nToken = cfg.n8nToken;
    e2eInstance = cfg.instanceName;
    const probe = await apiGetAsN8n('/api/Tenants/GetContextByInstance', cfg.tenantId, n8nToken, {
      instanceName: e2eInstance,
    });
    if (probe.status === 401) {
      throw new Error(
        `E2E_N8N_TOKEN / instance uyuşmuyor (tenant=${cfg.tenantId}, instance=${e2eInstance}).`,
      );
    }
    expect(probe.status, JSON.stringify(probe.json)).toBe(200);
    const mega = probe.json as { planType?: string; PlanType?: string };
    planType = String(mega.planType ?? mega.PlanType ?? '');
    preferredStaffId = cfg.staffId;
    googleStaffId = await resolveStaffIdWithGoogleToken(
      cfg.tenantId,
      e2eInstance,
      n8nToken,
      preferredStaffId,
    );
    if (googleStaffId) cfg = { ...cfg, staffId: googleStaffId };
  });

  test('GetContextByInstance → işletme bağlamı', async () => {
    const res = await apiGetAsN8n('/api/Tenants/GetContextByInstance', cfg.tenantId, n8nToken, {
      instanceName: e2eInstance,
    });
    expect(res.status, JSON.stringify(res.json)).toBe(200);
    const body = res.json as Record<string, unknown>;
    const tid = body.tenantId ?? body.TenantID ?? body.tenantID;
    expect(Number(tid)).toBe(cfg.tenantId);
    expect(body.services ?? body.Services).toBeTruthy();
  });

  test('available-slots → açık bir İstanbul gününde HH:mm listesi', async () => {
    let foundOpenDay = false;
    for (let offset = 14; offset < 45; offset++) {
      const date = istanbulYmdPlusDays(offset);
      if (weekdayOfYmd(date) === 0) continue;

      const { status, body } = await availableSlotsFor(date);
      expect(status, JSON.stringify(body)).toBe(200);

      if (body.isHoliday === true) {
        expect(slotTimes(body)).toEqual([]);
        continue;
      }

      const times = slotTimes(body);
      const total = Number(body.totalSlots ?? body.TotalSlots ?? times.length);
      expect(times.length, `${date} açık günde slot dönmeli`).toBeGreaterThan(0);
      expect(total).toBe(times.length);
      for (const t of times) {
        expect(t, `${date} slot formatı`).toMatch(/^\d{2}:\d{2}$/);
      }
      foundOpenDay = true;
      break;
    }
    expect(foundOpenDay, '14-45 gün içinde açık bir İstanbul günü bulunamadı').toBeTruthy();
  });

  test('WhatsAppBlockedPhones/check → gri liste sorgusu', async () => {
    const phone = '905320000199';
    const res = await apiGetAsN8n('/api/WhatsAppBlockedPhones/check', cfg.tenantId, n8nToken, {
      phone,
      tenantId: String(cfg.tenantId),
      instanceName: e2eInstance,
    });
    expect(res.status, JSON.stringify(res.json)).toBe(200);
    const body = res.json as { blocked?: boolean };
    expect(typeof body.blocked).toBe('boolean');
  });

  test('reminders/pending → yarın İstanbul penceresi ve plan kuralı', async () => {
    skipUnlessGoogleStaff(googleStaffId, preferredStaffId);
    const token = getN8nSystemAuthToken();
    test.skip(!token, 'E2E_N8N_SYSTEM_TOKEN veya API WebhookSecurity:N8nAuthToken gerekli');

    const emptyQueue = await apiGetAsN8n('/api/Appointments/reminders/pending', cfg.tenantId, token);
    expect(emptyQueue.status, JSON.stringify(emptyQueue.json)).toBe(200);
    expect(Array.isArray(emptyQueue.json)).toBe(true);

    const pendingShape = emptyQueue.json as Array<Record<string, unknown>>;
    for (const item of pendingShape) {
      expect(Number(item.appointmentId ?? item.AppointmentId)).toBeGreaterThan(0);
      expect(Number(item.tenantId ?? item.TenantId)).toBeGreaterThan(0);
      expect(String(item.customerPhone ?? item.CustomerPhone ?? '')).not.toBe('');
      expect(String(item.messageText ?? item.MessageText ?? '')).not.toBe('');
    }

    const tomorrow = istanbulYmdPlusDays(1);
    const { status: slotStatus, body: slotBody } = await availableSlotsFor(tomorrow);
    expect(slotStatus, JSON.stringify(slotBody)).toBe(200);

    const times = slotBody.isHoliday === true ? [] : slotTimes(slotBody);
    if (times.length === 0) {
      return;
    }

    const phone = `5320000${String(Date.now() % 1000).padStart(3, '0')}`;
    const slotTime = times[0].slice(0, 5);
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: 'Esse Reminder Live',
        customerPhone: phone,
        businessPhone: e2eInstance,
        serviceID: cfg.serviceId,
        appUserID: cfg.staffId,
        startDate: `${tomorrow}T${slotTime}:00`,
      },
      n8nToken,
      cfg.tenantId,
    );

    try {
      const after = await apiGetAsN8n('/api/Appointments/reminders/pending', cfg.tenantId, token);
      expect(after.status, JSON.stringify(after.json)).toBe(200);
      const list = after.json as Array<Record<string, unknown>>;
      const hit = list.find(
        (x) => Number(x.appointmentId ?? x.AppointmentId) === appointmentId,
      );

      if (canPlanUseReminders(planType)) {
        expect(hit, 'Pro/Business yarınki randevu pending kuyruğunda olmalı').toBeTruthy();
        const msg = String(hit?.messageText ?? hit?.MessageText ?? '');
        const [y, m, d] = tomorrow.split('-');
        expect(msg).toContain(`${d}.${m}.${y}`);
        expect(msg).toContain(slotTime);
      } else {
        expect(hit, 'Trial/Starter randevusu reminder kuyruğuna girmemeli').toBeFalsy();
        const mine = await fetchMyActiveAppointments(cfg.tenantId, e2eInstance, n8nToken, phone);
        expect(mine.status).toBe(200);
        expect(activeAppointmentIds(mine.body)).toContain(appointmentId);
      }
    } finally {
      await deleteAppointmentAsN8n(appointmentId, n8nToken, cfg.tenantId);
    }
  });

  test('PUT /api/Appointments/{id} → canlı slot değiştirir', async () => {
    skipUnlessGoogleStaff(googleStaffId, preferredStaffId);
    const phone = `5320000${String(Date.now() % 1000).padStart(3, '0')}`;
    const booking = await resolveE2eBookingContext(
      cfg.tenantId,
      e2eInstance,
      n8nToken,
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
      n8nToken,
      cfg.tenantId,
    );

    try {
      const { status, body } = await availableSlotsFor(booking.slotDate);
      expect(status, JSON.stringify(body)).toBe(200);
      const times = slotTimes(body).map((t) => t.slice(0, 5));
      const nextTime = times.find((t) => t > booking.slotTime);
      expect(nextTime, `${booking.slotDate} için ikinci müsait saat olmalı`).toBeTruthy();

      const newIso = `${booking.slotDate}T${nextTime}:00`;
      const put = await apiPutAsN8n(`/api/Appointments/${appointmentId}`, cfg.tenantId, n8nToken, {
        customerName: 'Esse Contract Upd',
        customerPhone: phone,
        serviceID: cfg.serviceId,
        appUserID: cfg.staffId,
        startDate: newIso,
        businessPhone: e2eInstance,
      });
      expect(put.status, JSON.stringify(put.json)).toBe(200);

      const mine = await fetchMyActiveAppointments(cfg.tenantId, e2eInstance, n8nToken, phone);
      expect(mine.status, JSON.stringify(mine.body)).toBe(200);
      expect(activeAppointmentIds(mine.body)).toContain(appointmentId);
      const row = (mine.body.appointments ?? []).find(
        (a) => Number(a.appointmentId ?? a.AppointmentId) === appointmentId,
      );
      const start = String(row?.startTime ?? row?.StartTime ?? '');
      expect(start).toContain(nextTime);
    } finally {
      await deleteAppointmentAsN8n(appointmentId, n8nToken, cfg.tenantId);
    }
  });

  test('DELETE /api/Appointments/{id} → canlı kaydı siler', async () => {
    skipUnlessGoogleStaff(googleStaffId, preferredStaffId);
    const phone = `5320000${String((Date.now() + 1) % 1000).padStart(3, '0')}`;
    const booking = await resolveE2eBookingContext(
      cfg.tenantId,
      e2eInstance,
      n8nToken,
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
      n8nToken,
      cfg.tenantId,
    );

    const before = await fetchMyActiveAppointments(cfg.tenantId, e2eInstance, n8nToken, phone);
    expect(before.status, JSON.stringify(before.body)).toBe(200);
    expect(activeAppointmentIds(before.body)).toContain(appointmentId);

    const del = await apiDeleteAsN8n(`/api/Appointments/${appointmentId}`, cfg.tenantId, n8nToken);
    expect(del.status, JSON.stringify(del.json)).toBe(200);

    const after = await fetchMyActiveAppointments(cfg.tenantId, e2eInstance, n8nToken, phone);
    expect(after.status, JSON.stringify(after.body)).toBe(200);
    expect(activeAppointmentIds(after.body)).not.toContain(appointmentId);
    expect(after.body.hasActiveAppointment).toBeFalsy();
  });

  test('my-active-appointments → oluştur, WhatsApp jid ile oku, sil', async () => {
    skipUnlessGoogleStaff(googleStaffId, preferredStaffId);
    const phone = `5320000${String((Date.now() + 2) % 1000).padStart(3, '0')}`;
    const jid = formatWhatsAppJid(phone);
    const booking = await resolveE2eBookingContext(
      cfg.tenantId,
      e2eInstance,
      n8nToken,
      cfg.serviceId,
      cfg.staffId,
    );
    const { appointmentId } = await createAppointmentAsN8n(
      {
        customerName: 'Esse Active Live',
        customerPhone: phone,
        businessPhone: e2eInstance,
        serviceID: cfg.serviceId,
        appUserID: cfg.staffId,
        startDate: booking.startIso,
      },
      n8nToken,
      cfg.tenantId,
    );

    try {
      const res = await fetchMyActiveAppointments(cfg.tenantId, e2eInstance, n8nToken, jid);
      expect(res.status, JSON.stringify(res.body)).toBe(200);
      expect(res.body.hasActiveAppointment).toBe(true);
      expect(activeAppointmentIds(res.body)).toContain(appointmentId);
      const row = (res.body.appointments ?? []).find(
        (a) => Number(a.appointmentId ?? a.AppointmentId) === appointmentId,
      );
      const start = String(row?.startTime ?? row?.StartTime ?? '');
      expect(start).toContain(booking.slotTime);
    } finally {
      await deleteAppointmentAsN8n(appointmentId, n8nToken, cfg.tenantId);
    }

    const after = await fetchMyActiveAppointments(cfg.tenantId, e2eInstance, n8nToken, jid);
    expect(after.status).toBe(200);
    expect(activeAppointmentIds(after.body)).not.toContain(appointmentId);
  });
});
