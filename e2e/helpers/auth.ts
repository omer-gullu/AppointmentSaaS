import { Browser, Page, expect, request as playwrightRequest } from '@playwright/test';
import { getEnvConfig } from './env';
import { dbConfigured, getOtpStateForPhone, waitForFreshOtpInDatabase } from './db';

export const AUTH_COOKIE_NAME = '.AspNetCore.Cookies';
export const PANEL_URL = '/Dashboard/Index';
export const OTP_VALIDITY_SECONDS = 90;
export const OTP_REQUEST_TIMEOUT_MS = Number(process.env.E2E_OTP_REQUEST_TIMEOUT_MS ?? 60_000);
export const OTP_RESEND_COOLDOWN_MS = Number(
  process.env.E2E_OTP_RESEND_COOLDOWN_MS ?? 46_000,
);

export type OtpSession = {
  page: Page;
  phone: string;
  /** Panel input ile aynı format (05xx…) */
  phoneForUi: string;
  otpCode: string;
};

/** API ile aynı normalizasyon (10 hane, başında 0/90 yok). */
export function normalizeLoginPhoneCore(phone: string): string {
  let d = phone.replace(/\D/g, '');
  if (d.startsWith('90') && d.length > 10) d = d.slice(2);
  if (d.startsWith('0') && d.length > 10) d = d.slice(1);
  return d.slice(-10);
}

/** Login input: 10–11 hane, kullanıcı alışkanlığı 05xxxxxxxxx */
export function formatPhoneForLoginInput(phone: string): string {
  const core = normalizeLoginPhoneCore(phone);
  return core.length === 10 ? `0${core}` : phone.replace(/\D/g, '');
}

export async function prepareOtpSession(browser: Browser, phoneNumber: string): Promise<OtpSession> {
  const manualCode = process.env.E2E_OTP_CODE?.trim();
  const phoneForUi = formatPhoneForLoginInput(phoneNumber);
  // test.use({ storageState }) varken browser.newPage() eksik dosyada patlar — temiz context
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  const { webUiBaseUrl } = getEnvConfig();
  await page.goto(`${webUiBaseUrl}/Auth/Login`);
  await page.waitForLoadState('domcontentloaded');

  const requestedAt = Date.now();
  await requestOtpAndWaitForStep2(page, phoneForUi);

  let otpCode = manualCode;
  if (!otpCode) {
    if (!dbConfigured()) {
      throw new Error('E2E_OTP_CODE veya E2E_DATABASE_URL gerekli (kod DB poll için).');
    }
    otpCode = await waitForFreshOtpInDatabase(phoneNumber, { requestedAfterMs: requestedAt });
  }

  return { page, phone: phoneNumber, phoneForUi, otpCode };
}

/** WebUI proxy yerine doğrudan API — Evolution gecikmesi UI timeout'unu tetiklemesin. */
export async function generateOtpViaApi(phoneNumber: string): Promise<void> {
  if (process.env.E2E_OTP_CODE?.trim()) return;

  const { apiBaseUrl } = getEnvConfig();
  const phoneForApi = formatPhoneForLoginInput(phoneNumber);
  const ctx = await playwrightRequest.newContext({ ignoreHTTPSErrors: true });

  try {
    for (let attempt = 0; attempt < 3; attempt++) {
      const res = await ctx.post(`${apiBaseUrl}/api/Auth/generate-otp`, {
        data: { phoneNumber: phoneForApi },
        timeout: OTP_REQUEST_TIMEOUT_MS,
      });

      if (res.status() === 429) {
        if (attempt >= 2) {
          throw new Error(
            `API generate-otp 429 (cooldown). ${OTP_RESEND_COOLDOWN_MS / 1000} sn bekleyip tekrar deneyin.`,
          );
        }
        await new Promise((r) => setTimeout(r, OTP_RESEND_COOLDOWN_MS));
        continue;
      }

      if (!res.ok()) {
        throw new Error(`API generate-otp (${res.status()}): ${await res.text()}`);
      }
      return;
    }
  } finally {
    await ctx.dispose();
  }
}

async function revealOtpStepOnLoginPage(page: Page, phoneForUi: string): Promise<void> {
  await page.locator('#phoneNumber').fill(phoneForUi);
  await page.evaluate((phone) => {
    const input = document.getElementById('phoneNumber') as HTMLInputElement | null;
    if (input) input.value = phone;
    const hidden = document.getElementById('loginPhone') as HTMLInputElement | null;
    if (hidden) hidden.value = phone;
    document.getElementById('step1')?.classList.add('d-none');
    document.getElementById('step2')?.classList.remove('d-none');
    const desc = document.getElementById('stepDescription');
    if (desc) desc.textContent = 'Giriş işlemini tamamlamak için kodu doğrulayın.';
    (document.querySelector('.otp-input') as HTMLInputElement | null)?.focus();
  }, phoneForUi);
  await page.locator('#loginPhone').fill(phoneForUi).catch(() => {});
  await expect(page.locator('#step2')).toBeVisible({ timeout: 10_000 });
  await expect(page.locator('#countdownTimer')).toBeVisible();
}

async function readLoginPageError(page: Page): Promise<string | null> {
  const err = page.locator('#errorMessage');
  if (!(await err.isVisible())) return null;
  return (await err.locator('span').innerText()).trim();
}

export async function requestOtpAndWaitForStep2(page: Page, phoneForUi: string): Promise<void> {
  await page.locator('#phoneNumber').fill(phoneForUi);

  if (process.env.E2E_OTP_UI_ONLY !== 'true') {
    await generateOtpViaApi(phoneForUi);
    await revealOtpStepOnLoginPage(page, phoneForUi);
    return;
  }

  const responsePromise = page.waitForResponse(
    (res) => res.url().includes('/Auth/GenerateOtp') && res.request().method() === 'POST',
    { timeout: OTP_REQUEST_TIMEOUT_MS },
  );
  await page.locator('#btnRequestOtp').click();
  const response = await responsePromise;

  if (!response.ok()) {
    const msg = (await readLoginPageError(page)) ?? `HTTP ${response.status()}`;
    throw new Error(`GenerateOtp failed (${response.status()}): ${msg}`);
  }

  await expect(page.locator('#step2')).toBeVisible({ timeout: 10_000 });
  await expect(page.locator('#countdownTimer')).toBeVisible();
}

/** VerifyOtp — başarı: HTTP 2xx + { success: true } veya panele yönlendirme. */
export async function clickVerifyOtpAndWait(page: Page, timeoutMs = 90_000) {
  const responsePromise = page.waitForResponse(
    (res) => res.url().includes('/Auth/VerifyOtp') && res.request().method() === 'POST',
    { timeout: timeoutMs },
  );
  await page.locator('#otpForm').evaluate((form: HTMLFormElement) => {
    form.requestSubmit();
  });
  return responsePromise;
}

export async function expectWrongOtpError(page: Page): Promise<void> {
  const response = await clickVerifyOtpAndWait(page);
  expect(response.ok(), 'Yanlış kod ile giriş başarılı olmamalı').toBeFalsy();
  await expect(page.locator('#errorMessage')).toHaveClass(/d-flex/);
  await expect(page.locator('#errorMessage span')).toContainText(
    /hatalı|süresi geçmiş|doğrulama|tamamlanamadı|geçersiz/i,
    { timeout: 5_000 },
  );
}

export async function fillOtp(page: Page, code: string): Promise<void> {
  const digits = code.replace(/\D/g, '').padStart(6, '0').slice(-6);
  const inputs = page.locator('.otp-input');
  for (let i = 0; i < 6; i++) {
    await inputs.nth(i).fill(digits[i] ?? '0');
  }
}

/** Adım 2'deyken #phoneNumber gizli — sadece #loginPhone (panel JS ile aynı). */
export async function syncLoginPhoneForVerify(page: Page, phoneForUi: string): Promise<void> {
  const step2Visible = await page.locator('#step2').isVisible();
  if (step2Visible) {
    await page.evaluate((phone) => {
      const hidden = document.getElementById('loginPhone') as HTMLInputElement | null;
      if (hidden) hidden.value = phone;
      const input = document.getElementById('phoneNumber') as HTMLInputElement | null;
      if (input) input.value = phone;
    }, phoneForUi);
    return;
  }
  await page.locator('#phoneNumber').fill(phoneForUi);
  await page.locator('#loginPhone').fill(phoneForUi).catch(() => {});
}

export async function loginWithOtp(
  page: Page,
  phoneForUi: string,
  otpCode: string,
): Promise<void> {
  await syncLoginPhoneForVerify(page, phoneForUi);

  let codeToUse = otpCode.replace(/\D/g, '').padStart(6, '0').slice(-6);
  if (!process.env.E2E_OTP_CODE?.trim() && dbConfigured()) {
    const row = await getOtpStateForPhone(phoneForUi);
    if (row?.OtpCode) {
      codeToUse = row.OtpCode.replace(/\D/g, '').padStart(6, '0').slice(-6);
    }
  }

  await fillOtp(page, codeToUse);

  let response = await clickVerifyOtpAndWait(page);
  const bodyText = await response.text().catch(() => '');
  let body: { success?: boolean; message?: string } = {};
  try {
    body = JSON.parse(bodyText) as { success?: boolean; message?: string };
  } catch {
    /* HTML hata sayfası */
  }

  if ((!response.ok() || body.success === false) && response.status() === 401 && dbConfigured()) {
    await new Promise((r) => setTimeout(r, 2000));
    const row = await getOtpStateForPhone(phoneForUi);
    if (row?.OtpCode) {
      const retryCode = row.OtpCode.replace(/\D/g, '').padStart(6, '0').slice(-6);
      if (retryCode !== codeToUse) {
        await fillOtp(page, retryCode);
        response = await clickVerifyOtpAndWait(page);
        const retryText = await response.text().catch(() => '');
        try {
          body = JSON.parse(retryText) as { success?: boolean; message?: string };
        } catch {
          body = {};
        }
      }
    }
  }

  if (!response.ok() || body.success === false) {
    const uiErr = await readLoginPageError(page);
    const headers = response.headers();
    const contentType = headers['content-type'] ?? headers['Content-Type'] ?? '';
    throw new Error(
      [
        `VerifyOtp başarısız (HTTP ${response.status()}):`,
        `UI='${uiErr ?? ''}'`,
        `content-type='${contentType}'`,
        `body='${body.success === false ? body.message ?? '' : bodyText.slice(0, 400)}'`,
      ].join(' '),
    );
  }

  await expect(page).toHaveURL(/\/Dashboard\/Index/i, { timeout: 60_000 });
  await page.waitForLoadState('domcontentloaded');
}

/** Askıdaki tenant: API generate-otp reddedilmeli (403/402). */
export async function expectGenerateOtpBlockedViaApi(phoneNumber: string): Promise<void> {
  const { apiBaseUrl } = getEnvConfig();
  const phoneForApi = formatPhoneForLoginInput(phoneNumber);
  const ctx = await playwrightRequest.newContext({ ignoreHTTPSErrors: true });
  try {
    const res = await ctx.post(`${apiBaseUrl}/api/Auth/generate-otp`, {
      data: { phoneNumber: phoneForApi },
      timeout: OTP_REQUEST_TIMEOUT_MS,
    });
    const text = await res.text();
    expect(res.ok(), text).toBeFalsy();
    expect(res.status(), text).toMatch(/403|402|429/);
  } finally {
    await ctx.dispose();
  }
}

/** Askıdaki tenant: WebUI GenerateOtp reddedilir (panel proxy). */
export async function expectSuspendedTenantOtpBlocked(page: Page, phoneNumber: string): Promise<void> {
  const phoneForUi = formatPhoneForLoginInput(phoneNumber);
  const { webUiBaseUrl } = getEnvConfig();
  await page.goto(`${webUiBaseUrl}/Auth/Login`);
  await page.waitForLoadState('domcontentloaded');
  await page.locator('#phoneNumber').fill(phoneForUi);

  const responsePromise = page.waitForResponse(
    (res) => res.url().includes('/Auth/GenerateOtp') && res.request().method() === 'POST',
    { timeout: OTP_REQUEST_TIMEOUT_MS },
  );
  await page.locator('#btnRequestOtp').click();
  const response = await responsePromise;
  const body = (await response.json().catch(() => ({}))) as { success?: boolean; message?: string };
  expect(body.success, body.message ?? `HTTP ${response.status()}`).not.toBe(true);
  expect(response.ok(), body.message ?? `HTTP ${response.status()}`).toBeFalsy();

  await expect(page.locator('#errorMessage')).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('#errorMessage span')).toContainText(
    /askı|abonelik|erişim|hizmet veremiyor|kapatılmış/i,
  );
  await expect(page.locator('#step2')).toBeHidden();
}

/** Randevu/ödeme testleri: API OTP + taze DB kodu + WebUI VerifyOtp (çerez). */
export async function loginWithOtpOnPage(page: Page, phoneNumber: string): Promise<void> {
  const phoneForUi = formatPhoneForLoginInput(phoneNumber);
  const { webUiBaseUrl } = getEnvConfig();
  await page.goto(`${webUiBaseUrl}/Auth/Login`);
  await page.waitForLoadState('domcontentloaded');

  const requestedAt = Date.now();
  await requestOtpAndWaitForStep2(page, phoneForUi);

  const code =
    process.env.E2E_OTP_CODE?.trim() ??
    (await waitForFreshOtpInDatabase(phoneNumber, { requestedAfterMs: requestedAt }));

  await loginWithOtp(page, phoneForUi, code);
}

export async function expectAuthCookie(page: Page): Promise<void> {
  const cookies = await page.context().cookies();
  const auth = cookies.find((c) => c.name === AUTH_COOKIE_NAME);
  expect(auth, `Expected auth cookie "${AUTH_COOKIE_NAME}"`).toBeTruthy();
  expect(auth!.value.length).toBeGreaterThan(10);
}

/** Tek seferlik manager storageState (panel testleri). */
export async function saveManagerStorageState(
  browser: Browser,
  phoneNumber: string,
  authFilePath: string,
): Promise<void> {
  const session = await prepareOtpSession(browser, phoneNumber);
  await loginWithOtp(session.page, session.phoneForUi, session.otpCode);
  await session.page.context().storageState({ path: authFilePath });
  await session.page.close();
}
