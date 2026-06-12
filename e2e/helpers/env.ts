export type PlaywrightEnv = 'development' | 'staging' | 'production';

export interface EnvConfig {
  name: PlaywrightEnv;
  webUiBaseUrl: string;
  apiBaseUrl: string;
  readonly: boolean;
  /** Only @smoke tests run in production */
  smokeOnly: boolean;
}

const profiles: Record<PlaywrightEnv, Omit<EnvConfig, 'name'>> = {
  development: {
    webUiBaseUrl: process.env.E2E_WEB_UI_URL ?? 'https://localhost:7140',
    apiBaseUrl: process.env.E2E_API_URL ?? 'http://localhost:5294',
    readonly: false,
    smokeOnly: false,
  },
  staging: {
    webUiBaseUrl: process.env.E2E_WEB_UI_URL ?? process.env.STAGING_WEB_UI_URL ?? '',
    apiBaseUrl: process.env.E2E_API_URL ?? process.env.STAGING_API_URL ?? '',
    readonly: false,
    smokeOnly: false,
  },
  production: {
    webUiBaseUrl: process.env.E2E_WEB_UI_URL ?? process.env.PRODUCTION_WEB_UI_URL ?? '',
    apiBaseUrl: process.env.E2E_API_URL ?? process.env.PRODUCTION_API_URL ?? '',
    readonly: true,
    smokeOnly: true,
  },
};

export function getPlaywrightEnv(): PlaywrightEnv {
  const raw = (process.env.PLAYWRIGHT_ENV ?? 'development').toLowerCase();
  if (raw === 'staging' || raw === 'production') return raw;
  return 'development';
}

export function getEnvConfig(): EnvConfig {
  const name = getPlaywrightEnv();
  const profile = profiles[name];
  if (!profile.webUiBaseUrl || !profile.apiBaseUrl) {
    throw new Error(
      `PLAYWRIGHT_ENV=${name} requires E2E_WEB_UI_URL and E2E_API_URL (or STAGING_/PRODUCTION_ variants).`,
    );
  }
  return { name, ...profile };
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
