import { defineConfig, type ReporterDescription } from '@playwright/test';

const isCI = Boolean(process.env.CI);
const apiBaseUrl = (process.env.E2E_API_URL ?? 'http://127.0.0.1:5294').replace(/\/+$/, '');

/** Isolated API CRUD gate — no panel OTP, no live n8n/WhatsApp, no dotenv (prod URL leak). */
export default defineConfig({
  testDir: './tests',
  testMatch: 'appointment-gate.spec.ts',
  timeout: 90_000,
  fullyParallel: false,
  forbidOnly: isCI,
  retries: 0,
  workers: 1,
  reporter: (() => {
    const reporters: ReporterDescription[] = [
      ['list'],
      ['html', { open: 'never', outputFolder: 'playwright-report-gate' }],
    ];
    if (isCI) reporters.push(['github']);
    return reporters;
  })(),
  use: {
    ignoreHTTPSErrors: true,
    actionTimeout: 30_000,
  },
  projects: [{ name: 'api-gate' }],
  outputDir: 'test-results-gate',
  metadata: {
    suite: 'appointment-crud-gate',
    apiBaseUrl,
  },
});
