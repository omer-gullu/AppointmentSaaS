import { dbConfigured } from './db';
import { panelTestsEnabled } from './e2e-config';
import { isReadonlyEnv } from './env';

/** API webhook billing testleri için eksik .env anahtarları. */
export function missingBillingApiEnv(): string[] {
  const missing: string[] = [];
  if (isReadonlyEnv()) missing.push('PLAYWRIGHT_ENV≠production (readonly)');
  if (!dbConfigured()) missing.push('E2E_DB_SERVER veya E2E_DATABASE_URL');
  if (!process.env.IYZICO_WEBHOOK_SECRET?.trim()) {
    missing.push('IYZICO_WEBHOOK_SECRET (API appsettings Iyzico:WebhookSecret ile aynı)');
  }
  if (!Number(process.env.E2E_TENANT_ID ?? '0')) missing.push('E2E_TENANT_ID');
  return missing;
}

/** Panel ChangePlan testleri için eksik ayarlar. */
export function missingBillingPanelEnv(): string[] {
  const missing: string[] = [];
  if (!panelTestsEnabled()) missing.push('E2E_RUN_PANEL_TESTS=true');
  if (!process.env.E2E_MANAGER_PHONE?.trim()) missing.push('E2E_MANAGER_PHONE');
  return missing;
}
