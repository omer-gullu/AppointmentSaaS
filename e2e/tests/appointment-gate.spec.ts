import { test, expect } from '@playwright/test';
import {
  createAppointmentAsN8n,
  deleteAppointmentAsN8n,
  updateAppointmentAsN8n,
} from '../helpers/appointments';
import {
  apiGetAsN8n,
  fetchMyActiveAppointments,
  findActiveAppointment,
  resolveE2eBookingContext,
  resolveLaterSlotIso,
} from '../helpers/n8n';

const API_URL = (process.env.E2E_API_URL ?? '').trim();
const TOKEN = (process.env.E2E_N8N_TOKEN ?? 'JNT-123-ABC').trim();
const TENANT_ID = Number(process.env.E2E_TENANT_ID ?? '1');
const INSTANCE = (process.env.E2E_INSTANCE_NAME ?? 'ci-janti').trim();
const SERVICE_ID = Number(process.env.E2E_SERVICE_ID ?? '1');
const STAFF_ID = Number(process.env.E2E_STAFF_ID ?? '101');

test.describe.configure({ mode: 'serial' });

test.describe('API randevu CRUD kapısı (izole Postgres)', () => {
  test.beforeAll(() => {
    if (!API_URL) {
      throw new Error('E2E_API_URL gerekli — bu kapı canlı API/DB kullanmaz.');
    }
  });

  test('GetContextByInstance + available-slots + create/update/delete', async () => {
    const ctx = await apiGetAsN8n('/api/Tenants/GetContextByInstance', TENANT_ID, TOKEN, {
      instanceName: INSTANCE,
    });
    expect(ctx.status, JSON.stringify(ctx.json)).toBe(200);

    const mega = ctx.json as {
      tenantID?: number;
      TenantID?: number;
      services?: { id?: number; Id?: number }[];
      Services?: { id?: number; Id?: number }[];
      staffs?: { id?: number; Id?: number }[];
      Staffs?: { id?: number; Id?: number }[];
    };
    expect(Number(mega.tenantID ?? mega.TenantID)).toBe(TENANT_ID);

    const services = mega.services ?? mega.Services ?? [];
    const staffs = mega.staffs ?? mega.Staffs ?? [];
    expect(services.some((s) => (s.id ?? s.Id) === SERVICE_ID)).toBeTruthy();
    expect(staffs.some((s) => (s.id ?? s.Id) === STAFF_ID)).toBeTruthy();

    const booking = await resolveE2eBookingContext(
      TENANT_ID,
      INSTANCE,
      TOKEN,
      SERVICE_ID,
      STAFF_ID,
    );

    const phone = '05320000001';
    const name = 'Esse Gate Mut';
    const created = await createAppointmentAsN8n(
      {
        customerName: name,
        customerPhone: phone,
        businessPhone: INSTANCE,
        serviceID: SERVICE_ID,
        appUserID: STAFF_ID,
        startDate: booking.startIso,
      },
      TOKEN,
      TENANT_ID,
    );
    expect(created.appointmentId).toBeGreaterThan(0);

    const afterCreate = await fetchMyActiveAppointments(TENANT_ID, INSTANCE, TOKEN, phone);
    expect(afterCreate.status, JSON.stringify(afterCreate.body)).toBe(200);
    expect(findActiveAppointment(afterCreate.body, created.appointmentId)).toBeTruthy();

    const laterIso = await resolveLaterSlotIso(
      TENANT_ID,
      INSTANCE,
      TOKEN,
      STAFF_ID,
      booking,
    );
    const updated = await updateAppointmentAsN8n(
      created.appointmentId,
      {
        customerName: name,
        customerPhone: phone,
        serviceID: SERVICE_ID,
        appUserID: STAFF_ID,
        startDate: laterIso,
        businessPhone: INSTANCE,
      },
      TOKEN,
      TENANT_ID,
    );
    expect(updated.status, JSON.stringify(updated.json)).toBe(200);

    const deleted = await deleteAppointmentAsN8n(created.appointmentId, TOKEN, TENANT_ID);
    expect(deleted.status, JSON.stringify(deleted.json)).toBe(200);

    const afterDelete = await fetchMyActiveAppointments(TENANT_ID, INSTANCE, TOKEN, phone);
    expect(afterDelete.status).toBe(200);
    expect(findActiveAppointment(afterDelete.body, created.appointmentId)).toBeFalsy();
  });
});
