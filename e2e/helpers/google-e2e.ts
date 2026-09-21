import { test } from '@playwright/test';
import { getEnvConfig } from './env';
import { apiGetAsN8n } from './n8n';
import { getGoogleAccessTokenViaWebhook } from './webhooks';

function staffIdsFromMega(json: unknown, preferred: number): number[] {
  const mega = (json ?? {}) as {
    staffs?: { id?: number; Id?: number }[];
    Staffs?: { id?: number; Id?: number }[];
  };
  const listed = (mega.staffs ?? mega.Staffs ?? [])
    .map((s) => Number(s.id ?? s.Id ?? 0))
    .filter((id) => id > 0);
  return [...new Set([preferred, ...listed].filter((id) => id > 0))];
}

function hasAccessToken(json: unknown): boolean {
  const body = (json ?? {}) as { accessToken?: string; AccessToken?: string };
  return Boolean((body.accessToken ?? body.AccessToken ?? '').trim());
}

/** GetGoogleAccessToken 200 + access token olan ilk personel. Token değerini loglama. */
export async function resolveStaffIdWithGoogleToken(
  tenantId: number,
  instanceName: string,
  token: string,
  preferredStaffId: number,
): Promise<number | null> {
  const ctx = await apiGetAsN8n('/api/Tenants/GetContextByInstance', tenantId, token, {
    instanceName,
  });
  if (ctx.status !== 200) return null;

  for (const staffId of staffIdsFromMega(ctx.json, preferredStaffId)) {
    const res = await getGoogleAccessTokenViaWebhook(instanceName, token, staffId);
    if (res.status === 200 && hasAccessToken(res.json)) return staffId;
  }
  return null;
}

export function googleConnectSkipMessage(staffId: number): string {
  const web = getEnvConfig().webUiBaseUrl.replace(/\/+$/, '');
  return (
    `E2E personelinde Google Takvim yok (API randevu oluşturmayı reddediyor). ` +
    `Panel: ${web}/Dashboard/ConnectStaffGoogle?staffId=${staffId} — bağladıktan sonra bu testleri tekrar çalıştır.`
  );
}

export function skipUnlessGoogleStaff(staffId: number | null, fallbackStaffId: number): void {
  test.skip(!staffId, googleConnectSkipMessage(fallbackStaffId));
}
