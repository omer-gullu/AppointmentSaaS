import { getEnvConfig } from './env';

/** Statik E2E: tüm kimlik bilgileri .env'den (discover-env.ps1 ile doldurulur). */
export type E2eStaticConfig = {
  tenantId: number;
  instanceName: string;
  serviceId: number;
  staffId: number;
  n8nToken: string;
  managerPhone: string;
  subscriptionRef: string;
  apiBaseUrl: string;
  webUiBaseUrl: string;
};

const PLACEHOLDER_INSTANCE = /^(your_|changeme|example|test_instance|placeholder)/i;

export function isPlaceholderEnvValue(value: string | undefined): boolean {
  const v = (value ?? '').trim();
  if (!v) return true;
  if (v === '0') return true;
  return PLACEHOLDER_INSTANCE.test(v);
}

/** Panel OTP login testleri (varsayılan kapalı). */
export function panelTestsEnabled(): boolean {
  return process.env.E2E_RUN_PANEL_TESTS === 'true';
}

export function getE2eStaticConfig(): E2eStaticConfig | null {
  const tenantId = Number(process.env.E2E_TENANT_ID ?? '0');
  const instanceName = process.env.E2E_INSTANCE_NAME?.trim() ?? '';
  const serviceId = Number(process.env.E2E_SERVICE_ID ?? '0');
  const staffId = Number(process.env.E2E_STAFF_ID ?? '0');
  const n8nToken =
    process.env.E2E_N8N_TOKEN?.trim() ?? process.env.N8N_AUTH_TOKEN?.trim() ?? '';

  if (tenantId <= 0 || serviceId <= 0 || staffId <= 0) return null;
  if (isPlaceholderEnvValue(instanceName)) return null;
  if (!n8nToken) return null;

  const { apiBaseUrl, webUiBaseUrl } = getEnvConfig();
  return {
    tenantId,
    instanceName,
    serviceId,
    staffId,
    n8nToken,
    managerPhone: process.env.E2E_MANAGER_PHONE?.trim() ?? '',
    subscriptionRef: process.env.E2E_SUBSCRIPTION_REF?.trim() ?? '',
    apiBaseUrl,
    webUiBaseUrl,
  };
}

export function requireE2eStaticConfig(): E2eStaticConfig {
  const cfg = getE2eStaticConfig();
  if (cfg) return cfg;
  throw new Error(
    'E2E statik yapılandırma eksik veya şablon değer. e2e/scripts/discover-env.ps1 → e2e/.env: E2E_TENANT_ID, E2E_INSTANCE_NAME, E2E_SERVICE_ID, E2E_STAFF_ID, E2E_N8N_TOKEN',
  );
}
