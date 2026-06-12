import path from 'path';
import { config as loadEnv } from 'dotenv';
import { defineConfig, devices, type ReporterDescription } from '@playwright/test';
import { getEnvConfig, getPlaywrightEnv } from './helpers/env';

loadEnv({ path: path.join(__dirname, '.env') });

const env = getEnvConfig();
const isCI = Boolean(process.env.CI);

/**
 * Ortamlar:
 * - development (varsayılan): tam E2E, yerel URL'ler
 * - staging: E2E_WEB_UI_URL / E2E_API_URL zorunlu
 * - production: sadece @smoke, readonly (mutasyon testleri config'te filtrelenir)
 */
export default defineConfig({
  testDir: './tests',
  globalSetup: require.resolve('./global-setup'),
  timeout: 120_000,
  fullyParallel: false,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  workers: env.readonly ? 1 : isCI ? 2 : 1,
  reporter: (() => {
    const reporters: ReporterDescription[] = [
      ['list'],
      ['html', { open: 'never', outputFolder: 'playwright-report' }],
    ];
    if (isCI) reporters.push(['github']);
    return reporters;
  })(),
  use: {
    baseURL: env.webUiBaseUrl,
    trace: isCI ? 'on-first-retry' : 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'on-first-retry',
    ignoreHTTPSErrors: true,
    actionTimeout: 30_000,
    navigationTimeout: 45_000,
  },
  projects: [
    {
      name: getPlaywrightEnv(),
      use: {
        ...devices['Desktop Chrome'],
        extraHTTPHeaders: env.readonly
          ? { 'X-E2E-Readonly': '1' }
          : undefined,
      },
    },
  ],
  grep: env.smokeOnly ? /@smoke/ : undefined,
  grepInvert: env.smokeOnly ? /@destructive/ : undefined,
  outputDir: 'test-results',
  metadata: {
    environment: env.name,
    apiBaseUrl: env.apiBaseUrl,
    readonly: String(env.readonly),
  },
});
