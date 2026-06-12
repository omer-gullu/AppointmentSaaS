import { createHmac } from 'crypto';
import { request as playwrightRequest } from '@playwright/test';
import { getEnvConfig } from './env';

export function signIyzicoWebhookBody(body: string, secret: string): string {
  return createHmac('sha256', secret).update(body, 'utf8').digest('hex').toLowerCase();
}

export async function postIyzicoWebhook(payload: Record<string, unknown>): Promise<{
  status: number;
  body: string;
}> {
  const { apiBaseUrl } = getEnvConfig();
  const secret = process.env.IYZICO_WEBHOOK_SECRET?.trim();
  if (!secret) throw new Error('IYZICO_WEBHOOK_SECRET is required for payment webhook tests.');

  const body = JSON.stringify(payload);
  const signature = signIyzicoWebhookBody(body, secret);
  const ctx = await playwrightRequest.newContext({ ignoreHTTPSErrors: true });
  const response = await ctx.post(`${apiBaseUrl}/api/iyzico/webhook`, {
    headers: {
      'Content-Type': 'application/json',
      'X-Iyzico-Signature': signature,
    },
    data: body,
  });
  const text = await response.text();
  await ctx.dispose();
  return { status: response.status(), body: text };
}

export async function postN8nAppointment(
  payload: Record<string, unknown>,
  authToken: string,
  tenantId?: number,
): Promise<{
  status: number;
  json: unknown;
}> {
  const { apiBaseUrl } = getEnvConfig();
  const tid = tenantId ?? Number(process.env.E2E_TENANT_ID ?? 0);
  const instance = String(payload.businessPhone ?? process.env.E2E_INSTANCE_NAME ?? '');
  const qs = new URLSearchParams();
  if (instance) qs.set('instanceName', instance);
  const query = qs.toString();
  const url = `${apiBaseUrl}/api/Appointments${query ? `?${query}` : ''}`;

  const ctx = await playwrightRequest.newContext({ ignoreHTTPSErrors: true });
  const response = await ctx.post(url, {
    headers: {
      'Content-Type': 'application/json',
      'X-Auth-Token': authToken,
      ...(tid > 0 ? { 'X-Tenant-Id': String(tid) } : {}),
    },
    data: payload,
  });
  const json = await response.json().catch(() => ({}));
  await ctx.dispose();
  return { status: response.status(), json };
}

export async function getGoogleAccessTokenViaWebhook(
  instanceName: string,
  authToken: string,
  staffId?: number,
): Promise<{ status: number; json: unknown }> {
  const { apiBaseUrl } = getEnvConfig();
  const qs = new URLSearchParams({ instanceName });
  if (staffId) qs.set('staffId', String(staffId));
  const ctx = await playwrightRequest.newContext({ ignoreHTTPSErrors: true });
  const response = await ctx.get(`${apiBaseUrl}/api/Tenants/GetGoogleAccessToken?${qs}`, {
    headers: { 'X-Auth-Token': authToken },
  });
  const json = await response.json().catch(() => ({}));
  await ctx.dispose();
  return { status: response.status(), json };
}
