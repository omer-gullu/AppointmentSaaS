import { postN8nAppointment } from './webhooks';
import {
  apiDeleteAsN8n,
  apiGetAsN8n,
  apiPutAsN8n,
  fetchMyActiveAppointments,
  findActiveAppointment,
  type MyActiveAppointment,
} from './n8n';

export type AlternateIds = {
  serviceId: number;
  staffId: number;
  serviceName: string;
  staffName: string;
};

export type AppointmentApiLookup = {
  tenantId: number;
  token: string;
  instanceName: string;
  customerPhone: string;
};

function istanbulHm(value: string): string {
  const hasTz = /Z|[+-]\d{2}:\d{2}$/.test(value);
  const d = new Date(hasTz ? value : `${value.slice(0, 19)}+03:00`);
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/Istanbul',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).formatToParts(d);
  const hour = parts.find((p) => p.type === 'hour')?.value ?? '';
  const minute = parts.find((p) => p.type === 'minute')?.value ?? '';
  return `${hour}:${minute}`;
}

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
      payload.startDate = shiftSlotIso(payload.startDate, 30);
      continue;
    }
    break;
  }
  if (status !== 200) {
    throw new Error(`Appointment create failed (${status}): ${JSON.stringify(json)}`);
  }
  const id =
    Number(json.ID ?? json.id ?? json.appointmentId ?? json.AppointmentID ?? 0);
  if (!id) throw new Error(`Appointment ID missing from create response: ${JSON.stringify(json)}`);
  return { appointmentId: id, status };
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
  const wall = /Z|[+-]\d{2}:\d{2}$/.test(iso) ? iso : `${iso.slice(0, 19)}+03:00`;
  const d = new Date(wall);
  d.setTime(d.getTime() + minutes * 60_000);
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/Istanbul',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).formatToParts(d);
  const g = (t: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === t)?.value ?? '';
  return `${g('year')}-${g('month')}-${g('day')}T${g('hour')}:${g('minute')}:00`;
}

export function isoToPanelDate(iso: string): string {
  return iso.slice(0, 10);
}

export function isoToPanelTime(iso: string): string {
  const t = iso.includes('T') ? iso.split('T')[1] : iso;
  return t.slice(0, 5);
}

async function fetchActiveRow(
  appointmentId: number,
  lookup: AppointmentApiLookup,
): Promise<MyActiveAppointment | undefined> {
  const mine = await fetchMyActiveAppointments(
    lookup.tenantId,
    lookup.instanceName,
    lookup.token,
    lookup.customerPhone,
  );
  if (mine.status !== 200) {
    throw new Error(
      `my-active-appointments HTTP ${mine.status}: ${JSON.stringify(mine.body)}`,
    );
  }
  return findActiveAppointment(mine.body, appointmentId);
}

export async function waitForAppointmentViaApi(
  lookup: AppointmentApiLookup,
  timeoutMs = 30_000,
): Promise<MyActiveAppointment> {
  const deadline = Date.now() + timeoutMs;
  let lastStatus = 0;
  while (Date.now() < deadline) {
    const mine = await fetchMyActiveAppointments(
      lookup.tenantId,
      lookup.instanceName,
      lookup.token,
      lookup.customerPhone,
    );
    lastStatus = mine.status;
    const row = (mine.body.appointments ?? [])[0];
    if (mine.status === 200 && row) return row;
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(
    `Appointment not found via API for phone ${lookup.customerPhone} (last HTTP ${lastStatus})`,
  );
}

export async function appointmentExists(
  appointmentId: number,
  lookup: AppointmentApiLookup,
): Promise<boolean> {
  const mine = await fetchMyActiveAppointments(
    lookup.tenantId,
    lookup.instanceName,
    lookup.token,
    lookup.customerPhone,
  );
  if (mine.status !== 200) return false;
  return Boolean(findActiveAppointment(mine.body, appointmentId));
}

export async function assertAppointmentFields(
  appointmentId: number,
  expected: Partial<{
    serviceName: string;
    staffName: string;
    startIsoPrefix: string;
    customerName: string;
  }>,
  lookup: AppointmentApiLookup,
): Promise<void> {
  const row = await fetchActiveRow(appointmentId, lookup);
  if (!row) throw new Error(`Appointment ${appointmentId} not found via API`);

  const serviceName = String(row.serviceName ?? row.ServiceName ?? '');
  const staffName = String(row.staffName ?? row.StaffName ?? '');
  const customerName = String(row.customerName ?? row.CustomerName ?? '');
  const start = String(row.startTime ?? row.StartTime ?? '');

  if (expected.customerName != null && !customerName.match(new RegExp(expected.customerName, 'i'))) {
    throw new Error(`Expected customer ~${expected.customerName}, got ${customerName}`);
  }
  if (expected.serviceName != null && serviceName !== expected.serviceName) {
    throw new Error(`Expected service ${expected.serviceName}, got ${serviceName}`);
  }
  if (expected.staffName != null && staffName !== expected.staffName) {
    throw new Error(`Expected staff ${expected.staffName}, got ${staffName}`);
  }
  if (expected.startIsoPrefix != null) {
    const actual = istanbulHm(start);
    const want = istanbulHm(expected.startIsoPrefix);
    if (actual !== want) {
      throw new Error(`Expected StartDate ~${want}, got ${actual} (${start})`);
    }
  }
}
