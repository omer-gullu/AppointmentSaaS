import fs from 'fs';
import path from 'path';
import { expect, request as playwrightRequest } from '@playwright/test';
import { getE2eStaticConfig, isPlaceholderEnvValue } from './e2e-config';
import { dbConfigured, getTenantApiKey, getTenantE2eConfig } from './db';
import { getEnvConfig } from './env';

const API_REQUEST_TIMEOUT_MS = Number(process.env.E2E_API_REQUEST_TIMEOUT_MS ?? 90_000);

function apiRequestContext() {
  return playwrightRequest.newContext({
    ignoreHTTPSErrors: true,
    timeout: API_REQUEST_TIMEOUT_MS,
  });
}

/** Evolution API base (n8n: $('Evolution Webhook').first().json.body.server_url). */
export function resolveEvolutionBaseUrl(): string {
  const fromEnv =
    process.env.E2E_EVOLUTION_BASE_URL?.trim() ||
    process.env.EVOLUTION_API_URL?.trim() ||
    '';
  if (fromEnv) return fromEnv.replace(/\/$/, '');

  const devSettings = path.resolve(
    __dirname,
    '../../Appointment_SaaS.API/appsettings.Development.json',
  );
  if (fs.existsSync(devSettings)) {
    try {
      const json = JSON.parse(fs.readFileSync(devSettings, 'utf8')) as {
        EvolutionApi?: { BaseUrl?: string };
      };
      const base = json.EvolutionApi?.BaseUrl?.trim();
      if (base?.startsWith('http')) return base.replace(/\/$/, '');
    } catch {
      /* ignore */
    }
  }
  return '';
}

export function resolveEvolutionApiKey(): string {
  const fromEnv =
    process.env.E2E_EVOLUTION_API_KEY?.trim() ||
    process.env.EVOLUTION_API_KEY?.trim() ||
    '';
  if (fromEnv) return fromEnv;

  const devSettings = path.resolve(
    __dirname,
    '../../Appointment_SaaS.API/appsettings.Development.json',
  );
  if (fs.existsSync(devSettings)) {
    try {
      const json = JSON.parse(fs.readFileSync(devSettings, 'utf8')) as {
        EvolutionApi?: { GlobalApiKey?: string };
      };
      const key = json.EvolutionApi?.GlobalApiKey?.trim();
      if (key && !key.includes('YOUR_') && !key.includes('__EVOLUTION')) return key;
    } catch {
      /* ignore */
    }
  }
  return '';
}

export function getN8nAuthToken(): string {
  return (
    process.env.E2E_N8N_TOKEN?.trim() ??
    process.env.N8N_AUTH_TOKEN?.trim() ??
    ''
  );
}

/**
 * Tenant X-Auth-Token: varsayılan statik .env (E2E_N8N_TOKEN).
 * E2E_USE_DB_SECRETS=true ise Tenants.ApiKey DB'den okunur.
 */
export async function resolveN8nAuthToken(tenantId: number): Promise<string> {
  const staticCfg = getE2eStaticConfig();
  if (staticCfg && process.env.E2E_USE_DB_SECRETS !== 'true') {
    return staticCfg.n8nToken;
  }

  const fromEnv = getN8nAuthToken();
  if (fromEnv && process.env.E2E_USE_DB_SECRETS !== 'true') return fromEnv;

  if (dbConfigured() && tenantId > 0) {
    const fromDb = await getTenantApiKey(tenantId);
    if (fromDb) return fromDb;
  }
  return fromEnv;
}

/** Instance adı: varsayılan E2E_INSTANCE_NAME (.env). */
export async function resolveE2eInstanceName(tenantId: number, fromEnv?: string): Promise<string> {
  const env = (fromEnv ?? process.env.E2E_INSTANCE_NAME ?? '').trim();
  if (env && !isPlaceholderEnvValue(env)) return env;

  if (process.env.E2E_USE_DB_SECRETS === 'true' && dbConfigured() && tenantId > 0) {
    const cfg = await getTenantE2eConfig(tenantId);
    if (cfg?.instanceName) return cfg.instanceName;
  }
  return env;
}

/** Cron / reminders/pending — Tenant.ApiKey değil, API WebhookSecurity:N8nAuthToken. */
export function getN8nSystemAuthToken(): string {
  const fromEnv =
    process.env.E2E_N8N_SYSTEM_TOKEN?.trim() ||
    process.env.N8N_SYSTEM_AUTH_TOKEN?.trim() ||
    '';
  if (fromEnv) return fromEnv;

  const devSettings = path.resolve(
    __dirname,
    '../../Appointment_SaaS.API/appsettings.Development.json',
  );
  if (fs.existsSync(devSettings)) {
    try {
      const json = JSON.parse(fs.readFileSync(devSettings, 'utf8')) as {
        WebhookSecurity?: { N8nAuthToken?: string };
      };
      const key = json.WebhookSecurity?.N8nAuthToken?.trim();
      if (
        key &&
        !key.includes('YOUR_') &&
        !key.includes('__N8N') &&
        !key.includes('RASTGELE')
      ) {
        return key;
      }
    } catch {
      /* ignore */
    }
  }
  return '';
}

export function n8nAuthHeaders(tenantId: number, token: string): Record<string, string> {
  return {
    'X-Auth-Token': token,
    'X-Tenant-Id': String(tenantId),
  };
}

/** Tam URL veya E2E_N8N_BASE_URL + path (varsayılan webhook/evolution-webhook). */
export function getN8nEvolutionWebhookUrl(): string | null {
  const full = process.env.E2E_N8N_WEBHOOK_URL?.trim();
  if (full) return full;

  const base = process.env.E2E_N8N_BASE_URL?.trim();
  if (!base) return null;

  const path = process.env.E2E_N8N_WEBHOOK_PATH?.trim() || 'webhook/evolution-webhook';
  return `${base.replace(/\/$/, '')}/${path.replace(/^\//, '')}`;
}

export function formatWhatsAppJid(phone: string): string {
  let digits = phone.replace(/\D/g, '');
  if (digits.startsWith('0')) digits = `90${digits.slice(1)}`;
  if (digits.length === 10) digits = `90${digits}`;
  return `${digits}@s.whatsapp.net`;
}

/**
 * Evolution → n8n webhook POST gövdesi (MESSAGES_UPSERT).
 * n8n'de: $('Evolution Webhook').item.json.body → bu obje.
 * Redis vb.: $json.body.data.key.remoteJid ($json = webhook item.json iken).
 * İç içe ikinci bir "body" anahtarı YOK — o yüzden E2E'de fazladan body sarmalayıcı kullanılmaz.
 */
export function buildEvolutionWebhookPayload(options: {
  instanceName: string;
  customerPhone: string;
  messageText: string;
  pushName?: string;
  serverUrl?: string;
  apikey?: string;
}): Record<string, unknown> {
  const jid = formatWhatsAppJid(options.customerPhone);
  const senderDigits = jid.replace('@s.whatsapp.net', '');

  return {
    event: 'messages.upsert',
    instance: options.instanceName,
    server_url: options.serverUrl ?? resolveEvolutionBaseUrl(),
    apikey: options.apikey ?? resolveEvolutionApiKey(),
    data: {
      key: { remoteJid: jid, fromMe: false },
      sender: senderDigits,
      pushName: options.pushName ?? 'E2E Test',
      message: {
        conversation: options.messageText,
      },
    },
  };
}

export function isGeminiE2eEnabled(): boolean {
  return process.env.E2E_RUN_GEMINI === 'true';
}

export async function postEvolutionWebhookToN8n(
  payload: Record<string, unknown>,
  timeoutMs = Number(process.env.E2E_N8N_WEBHOOK_TIMEOUT_MS ?? 120_000),
): Promise<{ status: number; body: string }> {
  const url = getN8nEvolutionWebhookUrl();
  if (!url) {
    throw new Error('E2E_N8N_WEBHOOK_URL veya E2E_N8N_BASE_URL gerekli (canlı n8n testi).');
  }

  const serverUrl = String(payload.server_url ?? '').trim();
  if (!serverUrl.startsWith('http')) {
    throw new Error(
      [
        'E2E webhook payload server_url eksik — n8n WhatsApp URL şöyle kırılır: /message/sendText/...',
        'e2e/.env → E2E_EVOLUTION_BASE_URL=http://EVOLUTION_IP:8080',
        '(API appsettings.Development.json → EvolutionApi:BaseUrl ile aynı olmalı)',
      ].join('\n'),
    );
  }

  const ctx = await apiRequestContext();
  const response = await ctx.post(url, {
    headers: {
      'Content-Type': 'application/json',
      'ngrok-skip-browser-warning': '1',
    },
    data: payload,
    timeout: timeoutMs,
  });
  const body = await response.text();
  await ctx.dispose();
  return { status: response.status(), body };
}

export async function apiGetAsN8n(
  path: string,
  tenantId: number,
  token: string,
  query?: Record<string, string>,
): Promise<{ status: number; json: unknown }> {
  const { apiBaseUrl } = getEnvConfig();
  const qs = new URLSearchParams(query ?? {}).toString();
  const url = `${apiBaseUrl}${path}${qs ? `?${qs}` : ''}`;

  const ctx = await apiRequestContext();
  const response = await ctx.get(url, { headers: n8nAuthHeaders(tenantId, token) });
  const json = await response.json().catch(() => ({}));
  await ctx.dispose();
  return { status: response.status(), json };
}

export async function apiPostAsN8n(
  path: string,
  tenantId: number,
  token: string,
  body?: Record<string, unknown>,
): Promise<{ status: number; json: unknown }> {
  const { apiBaseUrl } = getEnvConfig();
  const ctx = await apiRequestContext();
  const response = await ctx.post(`${apiBaseUrl}${path}`, {
    headers: { ...n8nAuthHeaders(tenantId, token), 'Content-Type': 'application/json' },
    data: body ?? {},
  });
  const json = await response.json().catch(() => ({}));
  await ctx.dispose();
  return { status: response.status(), json };
}

export async function apiPutAsN8n(
  path: string,
  tenantId: number,
  token: string,
  body?: Record<string, unknown>,
): Promise<{ status: number; json: unknown }> {
  const { apiBaseUrl } = getEnvConfig();
  const ctx = await apiRequestContext();
  const response = await ctx.put(`${apiBaseUrl}${path}`, {
    headers: { ...n8nAuthHeaders(tenantId, token), 'Content-Type': 'application/json' },
    data: body ?? {},
  });
  const json = await response.json().catch(() => ({}));
  await ctx.dispose();
  return { status: response.status(), json };
}

export async function apiDeleteAsN8n(
  path: string,
  tenantId: number,
  token: string,
): Promise<{ status: number; json: unknown }> {
  const { apiBaseUrl } = getEnvConfig();
  const ctx = await apiRequestContext();
  const response = await ctx.delete(`${apiBaseUrl}${path}`, {
    headers: n8nAuthHeaders(tenantId, token),
  });
  const json = await response.json().catch(() => ({}));
  await ctx.dispose();
  return { status: response.status(), json };
}

export async function apiOptOutWhatsApp(
  tenantId: number,
  token: string,
  payload: { phone: string; tenantId?: number; instanceName: string; customerName?: string },
): Promise<{ status: number; json: unknown }> {
  return apiPostAsN8n('/api/WhatsAppBlockedPhones/opt-out', tenantId, token, payload);
}

export async function isPhoneBlocked(
  tenantId: number,
  token: string,
  phone: string,
  instanceName: string,
): Promise<boolean> {
  const res = await apiGetAsN8n('/api/WhatsAppBlockedPhones/check', tenantId, token, {
    phone,
    tenantId: String(tenantId),
    instanceName,
  });
  if (res.status !== 200) return false;
  return (res.json as { blocked?: boolean }).blocked === true;
}

export async function waitForPhoneBlocked(
  tenantId: number,
  token: string,
  phone: string,
  instanceName: string,
  timeoutMs = 90_000,
): Promise<void> {
  const jid = formatWhatsAppJid(phone);
  await expect
    .poll(() => isPhoneBlocked(tenantId, token, jid, instanceName), {
      timeout: timeoutMs,
      intervals: [1500, 2500, 4000],
    })
    .toBeTruthy();
}

export type E2eBookingContext = {
  serviceName: string;
  staffName: string;
  slotDate: string;
  slotTime: string;
  startIso: string;
};

type MegaService = { id?: number; Id?: number; name?: string; Name?: string };
type MegaStaff = { id?: number; Id?: number; fullName?: string; FullName?: string };
type MegaHoliday = { date?: string; Date?: string };

/** Randevu mesajı için müsait gün/saat + hizmet/personel adları (Gemini E2E). */
export async function resolveE2eBookingContext(
  tenantId: number,
  instanceName: string,
  token: string,
  serviceId: number,
  staffId: number,
): Promise<E2eBookingContext> {
  const ctxRes = await apiGetAsN8n('/api/Tenants/GetContextByInstance', tenantId, token, {
    instanceName,
  });
  if (ctxRes.status !== 200) {
    throw new Error(`GetContextByInstance failed: ${ctxRes.status} ${JSON.stringify(ctxRes.json)}`);
  }

  const mega = ctxRes.json as {
    services?: MegaService[];
    Services?: MegaService[];
    staffs?: MegaStaff[];
    Staffs?: MegaStaff[];
    upcomingHolidays?: MegaHoliday[];
    UpcomingHolidays?: MegaHoliday[];
  };

  const services = mega.services ?? mega.Services ?? [];
  const staffs = mega.staffs ?? mega.Staffs ?? [];
  const holidays = new Set(
    (mega.upcomingHolidays ?? mega.UpcomingHolidays ?? []).map((h) => h.date ?? h.Date ?? ''),
  );

  const service = services.find((s) => (s.id ?? s.Id) === serviceId);
  const staff = staffs.find((s) => (s.id ?? s.Id) === staffId);
  if (!service) throw new Error(`Service ${serviceId} not found in mega context`);
  if (!staff) throw new Error(`Staff ${staffId} not found in mega context`);

  const serviceName = service.name ?? service.Name ?? `Service ${serviceId}`;
  const staffName = staff.fullName ?? staff.FullName ?? `Staff ${staffId}`;

  const pad2 = (n: number) => String(n).padStart(2, '0');
  const localYmd = (d: Date) =>
    `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;

  for (let dayOffset = 14; dayOffset < 90; dayOffset++) {
    const d = new Date();
    d.setDate(d.getDate() + dayOffset);
    if (d.getDay() === 0) continue;

    const slotDate = localYmd(d);
    if (holidays.has(slotDate)) continue;

    const slotsRes = await apiGetAsN8n('/api/Appointments/available-slots', tenantId, token, {
      instanceName,
      staffId: String(staffId),
      date: slotDate,
      durationMinutes: '30',
    });
    if (slotsRes.status !== 200) continue;

    const body = slotsRes.json as Record<string, unknown>;
    if (body.isHoliday === true) continue;

    const rawSlots = body.availableSlots ?? body.AvailableSlots;
    const times: string[] = Array.isArray(rawSlots)
      ? rawSlots.map((t) => String(t).trim()).filter(Boolean)
      : [];
    if (times.length === 0) continue;

    const slotTime = times[0].length === 5 ? times[0] : times[0].slice(0, 5);
    const startIso = `${slotDate}T${slotTime}:00`;

    return { serviceName, staffName, slotDate, slotTime, startIso };
  }

  throw new Error('No available slot found in next 90 days for Gemini E2E');
}

export async function sendN8nCustomerMessage(options: {
  instanceName: string;
  customerPhone: string;
  messageText: string;
  pushName?: string;
}): Promise<{ status: number; body: string }> {
  const payload = buildEvolutionWebhookPayload(options);
  const timeout = Number(process.env.E2E_N8N_WEBHOOK_TIMEOUT_MS ?? 180_000);
  return postEvolutionWebhookToN8n(payload, timeout);
}

export function geminiMessageDelayMs(): number {
  return Number(process.env.E2E_GEMINI_MESSAGE_DELAY_MS ?? 30_000);
}

export function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** n8n webhook 500 "Error in workflow" için okunabilir hata metni. */
export function formatN8nWorkflowError(step: string, status: number, body: string): string {
  const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';
  const webhookUrl = getN8nEvolutionWebhookUrl() ?? '(tanımsız)';
  return [
    `n8n workflow başarısız — adım: ${step}`,
    `HTTP ${status}`,
    body.slice(0, 1000),
    '',
    'Kontrol listesi:',
    `• n8n → Executions → en son çalışma → hangi node kırmızı?`,
    `• Workflow HTTP URL'leri: YOUR-API-HOST kalmamış olmalı. n8n başka makinede ise localhost:5294 çalışmaz → API için de ngrok/public URL kullanın.`,
    `• Get Mega Context: X-Auth-Token = tenant integration key (E2E_N8N_TOKEN ile aynı)`,
    `• Gemini / Vertex AI credential n8n'de geçerli mi (ödeme / API key)`,
    `• API ayakta mı: ${apiUrl}`,
    `• Webhook URL: ${webhookUrl}`,
    `• Invalid URL /message/sendText → E2E_EVOLUTION_BASE_URL veya Evolution Webhook body.server_url boş`,
  ].join('\n');
}

export function assertN8nWebhookOk(
  res: { status: number; body: string },
  step: string,
): void {
  if (res.status >= 500 || res.body.includes('Error in workflow')) {
    throw new Error(formatN8nWorkflowError(step, res.status, res.body));
  }
  if (res.status >= 400) {
    throw new Error(formatN8nWorkflowError(step, res.status, res.body));
  }
}
