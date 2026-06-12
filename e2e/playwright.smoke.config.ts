import path from 'path';
import { config as loadEnv } from 'dotenv';
import { defineConfig, devices, type ReporterDescription } from '@playwright/test';
import { getEnvConfig, getPlaywrightEnv } from './helpers/env';

loadEnv({ path: path.join(__dirname, '.env') });

const env = getEnvConfig();
const isCI = Boolean(process.env.CI);

/** Panel OTP / global-setup olmadan yalnızca smoke.spec.ts */
export default defineConfig({
  testDir: './tests',
  testMatch: 'smoke.spec.ts',
  timeout: 60_000,
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  workers: 1,
  reporter: (() => {
    const reporters: ReporterDescription[] = [
      ['list'],
      ['html', { open: 'never', outputFolder: 'playwright-report-smoke' }],
    ];
    if (isCI) reporters.push(['github']);
    return reporters;
  })(),
  use: {
    baseURL: env.webUiBaseUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ignoreHTTPSErrors: true,
    actionTimeout: 30_000,
    navigationTimeout: 45_000,
  },
  projects: [
    {
      name: `${getPlaywrightEnv()}-smoke`,
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  outputDir: 'test-results-smoke',
  metadata: {
    environment: env.name,
    apiBaseUrl: env.apiBaseUrl,
    suite: 'smoke-only',
  },
});
