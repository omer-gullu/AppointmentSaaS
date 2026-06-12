import path from 'path';
import { chromium } from '@playwright/test';
import { config as loadEnv } from 'dotenv';
import { panelTestsEnabled } from './helpers/e2e-config';
import { ensureManagerStorageState, isManagerAuthReady } from './helpers/panel-auth';

loadEnv({ path: path.join(__dirname, '.env') });

export default async function globalSetup(): Promise<void> {
  if (!panelTestsEnabled()) return;

  const phone = process.env.E2E_MANAGER_PHONE?.trim();
  const tenantId = Number(process.env.E2E_TENANT_ID ?? '0');
  if (!phone) {
    console.warn('[e2e global-setup] E2E_RUN_PANEL_TESTS=true ama E2E_MANAGER_PHONE yok — panel OTP atlanır.');
    return;
  }

  const browser = await chromium.launch();
  try {
    console.log('[e2e global-setup] Manager OTP oturumu hazırlanıyor…');
    try {
      await ensureManagerStorageState(browser, phone, tenantId, { force: true });
    } catch (err) {
      console.warn(
        `[e2e global-setup] Panel OTP hazırlanamadı (${(err as Error).message}) — ` +
          'panel testleri skip olacak. sqlcmd/E2E_SQLCMD_PATH ve API+WebUI kontrol edin.',
      );
      return;
    }
    if (!isManagerAuthReady()) {
      console.warn(
        '[e2e global-setup] manager.json oluşturulamadı — panel testleri skip olacak. ' +
          'OTP 500 olabilir; API/WebUI ayakta mı kontrol edin.',
      );
      return;
    }
    console.log('[e2e global-setup] manager.json kaydedildi.');
  } finally {
    await browser.close();
  }
}
