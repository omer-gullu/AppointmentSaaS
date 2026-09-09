import path from 'path';
import { config as loadEnv } from 'dotenv';

loadEnv({ path: path.join(__dirname, '..', '.env') });

export type PlaywrightEnv = 'development' | 'staging' | 'production';

export interface EnvConfig {
  name: PlaywrightEnv;
  webUiBaseUrl: string;
  apiBaseUrl: string;
  readonly: boolean;
  /** Only @smoke tests run in production */
  smokeOnly: boolean;
}

export function getPlaywrightEnv(): PlaywrightEnv {
  const raw = (process.env.PLAYWRIGHT_ENV ?? 'development').toLowerCase();
  if (raw === 'staging' || raw === 'production') return raw;
  return 'development';
}

function stripTrailingSlash(url: string): string {
  return url.trim().replace(/\/+$/, '');
}

function envUrl(...keys: string[]): string {
  for (const key of keys) {
    const value = process.env[key]?.trim();
    if (value) return stripTrailingSlash(value);
  }
  return '';
}

export function getEnvConfig(): EnvConfig {
  const name = getPlaywrightEnv();
  const webUiBaseUrl =
    name === 'development'
      ? envUrl('E2E_WEB_UI_URL') || 'https://localhost:7140'
      : envUrl('E2E_WEB_UI_URL', name === 'staging' ? 'STAGING_WEB_UI_URL' : 'PRODUCTION_WEB_UI_URL');
  const apiBaseUrl =
    name === 'development'
      ? envUrl('E2E_API_URL') || 'http://localhost:5294'
      : envUrl('E2E_API_URL', name === 'staging' ? 'STAGING_API_URL' : 'PRODUCTION_API_URL');

  if (!webUiBaseUrl || !apiBaseUrl) {
    throw new Error(
      `PLAYWRIGHT_ENV=${name} requires E2E_WEB_UI_URL and E2E_API_URL (or STAGING_/PRODUCTION_ variants).`,
    );
  }

  return {
    name,
    webUiBaseUrl,
    apiBaseUrl,
    readonly: name === 'production',
    smokeOnly: name === 'production',
  };
}

export function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value?.trim()) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value.trim();
}

export function hasEnv(name: string): boolean {
  return Boolean(process.env[name]?.trim());
}

export function isReadonlyEnv(): boolean {
  return getEnvConfig().readonly;
}
