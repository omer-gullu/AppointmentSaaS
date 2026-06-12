import fs from 'fs';
import path from 'path';
import type { Browser, Page } from '@playwright/test';
import { loginWithOtp, loginWithOtpOnPage, PANEL_URL, prepareOtpSession } from './auth';
import { panelTestsEnabled } from './e2e-config';
import { ensureE2eTenantBillingActive } from './db';
import { getEnvConfig } from './env';

/** Tüm panel E2E spec'leri aynı oturumu paylaşır (tek OTP). */
export const MANAGER_AUTH_FILE = path.join(__dirname, '../playwright/.auth/manager.json');

const AUTH_MAX_AGE_MS = 45 * 60 * 1000;

export function isManagerAuthReady(): boolean {
  if (!fs.existsSync(MANAGER_AUTH_FILE)) return false;
  try {
    const age = Date.now() - fs.statSync(MANAGER_AUTH_FILE).mtimeMs;
    return age < AUTH_MAX_AGE_MS;
  } catch {
    return false;
  }
}

/**
 * Panel testleri için tek seferlik OTP + storageState.
 * global-setup veya describe beforeAll çağırabilir.
 */
export async function ensureManagerStorageState(
  browser: Browser,
  phone: string,
  tenantId: number,
  options?: { force?: boolean },
): Promise<void> {
  if (!panelTestsEnabled()) return;
  if (!phone?.trim()) {
    throw new Error('E2E_MANAGER_PHONE gerekli (panel oturumu).');
  }

  if (!options?.force && isManagerAuthReady()) return;

  if (tenantId > 0) await ensureE2eTenantBillingActive(tenantId);

  fs.mkdirSync(path.dirname(MANAGER_AUTH_FILE), { recursive: true });
  const session = await prepareOtpSession(browser, phone);
  await loginWithOtp(session.page, session.phoneForUi, session.otpCode);
  await session.page.context().storageState({ path: MANAGER_AUTH_FILE });
  await session.page.close();
}

/**
 * storageState süresi dolduysa login ekranına düşebiliriz.
 * Bu durumda tek seferlik OTP ile oturumu yenileyip hedef panele döner.
 */
export async function ensurePanelSession(page: Page, targetPath = PANEL_URL): Promise<void> {
  const { webUiBaseUrl } = getEnvConfig();
  const targetUrl = `${webUiBaseUrl}${targetPath}`;
  await page.goto(targetUrl);

  const loginPhoneInput = page.locator('input[name="phone"], #phoneNumber, input[placeholder*="5xx"]');
  if (await loginPhoneInput.first().isVisible().catch(() => false)) {
    const phone = process.env.E2E_MANAGER_PHONE?.trim();
    if (!phone) {
      throw new Error('E2E_MANAGER_PHONE gerekli: oturum düştüğünde panel yeniden login yapılamadı.');
    }
    await loginWithOtpOnPage(page, phone);
    await page.goto(targetUrl);
  }
}
