import { open } from 'k6/fs';

/** @returns {{ id: number, token: string, instanceName: string, serviceId: number, staffId: number }[]} */
export function loadTenantsFromEnv() {
  const raw = (__ENV.E2E_LOAD_TENANTS_JSON || '').trim();
  if (!raw) {
    throw new Error(
      'E2E_LOAD_TENANTS_JSON gerekli — dosya yolu veya JSON. discover-env.ps1 -ExportLoadTenants',
    );
  }

  let content = raw;
  if (!raw.startsWith('{') && !raw.startsWith('[')) {
    content = open(raw);
  }

  const parsed = JSON.parse(content);
  const list = Array.isArray(parsed) ? parsed : parsed.tenants;
  if (!list?.length) {
    throw new Error('E2E_LOAD_TENANTS_JSON: tenants dizisi boş');
  }

  for (const t of list) {
    if (!t.id || !t.token || !t.instanceName || !t.serviceId || !t.staffId) {
      throw new Error(`Geçersiz tenant: ${JSON.stringify(t)}`);
    }
  }
  return list;
}

export function authHeaders(tenantId, token) {
  return {
    'Content-Type': 'application/json',
    'X-Auth-Token': token,
    'X-Tenant-Id': String(tenantId),
  };
}

export function buildEvolutionPayload(instanceName, customerPhone, messageText) {
  let digits = customerPhone.replace(/\D/g, '');
  if (digits.startsWith('0')) digits = `90${digits.slice(1)}`;
  if (digits.length === 10) digits = `90${digits}`;
  const jid = `${digits}@s.whatsapp.net`;
  const serverUrl = (__ENV.E2E_EVOLUTION_BASE_URL || 'http://localhost:8080').trim();
  const apikey = (__ENV.E2E_EVOLUTION_APIKEY || 'e2e').trim();

  return {
    event: 'messages.upsert',
    instance: instanceName,
    server_url: serverUrl,
    apikey,
    data: {
      key: { remoteJid: jid, fromMe: false },
      sender: digits,
      pushName: 'K6 Load',
      message: { conversation: messageText },
    },
  };
}

export function futureSlotIso(daysAhead, hour, minute) {
  const d = new Date();
  d.setDate(d.getDate() + daysAhead);
  while (d.getDay() === 0) d.setDate(d.getDate() + 1);
  d.setHours(hour, minute, 0, 0);
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
}
