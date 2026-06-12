import { postN8nAppointment } from './webhooks';
import { apiDeleteAsN8n, apiGetAsN8n, apiPutAsN8n } from './n8n';
import { getAppointmentById } from './db';

export type AlternateIds = {
  serviceId: number;
  staffId: number;
  serviceName: string;
  staffName: string;
};

export async function createAppointmentAsN8n(
  payload: {
    customerName: string;
    customerPhone: string;
    businessPhone: string;
    serviceID: number;
    appUserID: number;
    startDate: string;
  },
  token: string,
  tenantId: number,
): Promise<{ appointmentId: number; status: number }> {
  let status = 0;
  let json: Record<string, unknown> = {};
  for (let attempt = 0; attempt < 6; attempt++) {
    const res = await postN8nAppointment(payload, token, tenantId);
    status = res.status;
    json = (res.json as Record<string, unknown>) ?? {};
    const msg = String(json.message ?? json.Message ?? '');
    if (status === 200) break;
    if (status === 400 && /çakış/i.test(msg) && attempt < 5) {
      const d = new Date(payload.startDate);
      d.setMinutes(d.getMinutes() + 5);
      payload.startDate = d.toISOString().slice(0, 19);
      continue;
    }
    break;
  }
  if (status !== 200) {
    throw new Error(`Appointment create failed (${status}): ${JSON.stringify(json)}`);
  }
  const id =
    Number(json.ID ?? json.id ?? json.appointmentId ?? json.AppointmentID ?? 0) ||
    (await getAppointmentByIdFromPhone(tenantId, payload.customerPhone));
  if (!id) throw new Error('Appointment ID missing from create response');
  return { appointmentId: id, status };
}

async function getAppointmentByIdFromPhone(tenantId: number, phone: string): Promise<number> {
  const { findAppointmentByCustomerPhone } = await import('./db');
  const row = await findAppointmentByCustomerPhone(tenantId, phone.slice(-8));
  return row?.AppointmentID ?? 0;
}

export async function updateAppointmentAsN8n(
  appointmentId: number,
  dto: {
    customerName: string;
    customerPhone: string;
    serviceID: number;
    appUserID: number;
    startDate: string;
    businessPhone: string;
  },
  token: string,
  tenantId: number,
): Promise<{ status: number; json: unknown }> {
  return apiPutAsN8n(`/api/Appointments/${appointmentId}`, tenantId, token, {
    customerName: dto.customerName,
    customerPhone: dto.customerPhone,
    serviceID: dto.serviceID,
    appUserID: dto.appUserID,
    startDate: dto.startDate,
    businessPhone: dto.businessPhone,
  });
}

export async function deleteAppointmentAsN8n(
  appointmentId: number,
  token: string,
  tenantId: number,
): Promise<{ status: number; json: unknown }> {
  return apiDeleteAsN8n(`/api/Appointments/${appointmentId}`, tenantId, token);
}

export async function resolveAlternateServiceAndStaff(
  tenantId: number,
  instanceName: string,
  token: string,
  currentServiceId: number,
  currentStaffId: number,
): Promise<AlternateIds | null> {
  const res = await apiGetAsN8n('/api/Tenants/GetContextByInstance', tenantId, token, {
    instanceName,
  });
  if (res.status !== 200) return null;

  const mega = res.json as {
    services?: { id?: number; Id?: number; name?: string; Name?: string }[];
    Services?: { id?: number; Id?: number; name?: string; Name?: string }[];
    staffs?: { id?: number; Id?: number; fullName?: string; FullName?: string }[];
    Staffs?: { id?: number; Id?: number; fullName?: string; FullName?: string }[];
  };

  const services = mega.services ?? mega.Services ?? [];
  const staffs = mega.staffs ?? mega.Staffs ?? [];

  const altService = services.find((s) => {
    const id = s.id ?? s.Id ?? 0;
    return id > 0 && id !== currentServiceId;
  });
  const altStaff = staffs.find((s) => {
    const id = s.id ?? s.Id ?? 0;
    return id > 0 && id !== currentStaffId;
  });

  if (!altService || !altStaff) return null;

  const serviceId = altService.id ?? altService.Id ?? 0;
  const staffId = altStaff.id ?? altStaff.Id ?? 0;
  return {
    serviceId,
    staffId,
    serviceName: altService.name ?? altService.Name ?? `Service ${serviceId}`,
    staffName: altStaff.fullName ?? altStaff.FullName ?? `Staff ${staffId}`,
  };
}

export function shiftSlotIso(iso: string, minutes: number): string {
  const d = new Date(iso);
  d.setMinutes(d.getMinutes() + minutes);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
}

export function isoToPanelDate(iso: string): string {
  return iso.slice(0, 10);
}

export function isoToPanelTime(iso: string): string {
  const t = iso.includes('T') ? iso.split('T')[1] : iso;
  return t.slice(0, 5);
}

export async function assertAppointmentFields(
  appointmentId: number,
  expected: Partial<{
    serviceId: number;
    staffId: number;
    startIsoPrefix: string;
  }>,
): Promise<void> {
  const row = await getAppointmentById(appointmentId);
  if (!row) throw new Error(`Appointment ${appointmentId} not found in DB`);
  if (expected.serviceId != null && row.ServiceID !== expected.serviceId) {
    throw new Error(`Expected ServiceID ${expected.serviceId}, got ${row.ServiceID}`);
  }
  if (expected.staffId != null && row.AppUserID !== expected.staffId) {
    throw new Error(`Expected AppUserID ${expected.staffId}, got ${row.AppUserID}`);
  }
  if (expected.startIsoPrefix != null) {
    const pad = (n: number) => String(n).padStart(2, '0');
    const fmt = (d: Date) =>
      `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    const actual = fmt(new Date(row.StartDate));
    const want = fmt(new Date(expected.startIsoPrefix));
    if (actual !== want) {
      throw new Error(`Expected StartDate ~${want}, got ${actual}`);
    }
  }
}
