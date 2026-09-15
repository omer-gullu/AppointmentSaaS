import { test, expect } from '@playwright/test';

/**
 * Pricing duman testleri — DB, sqlcmd ve panel OTP gerektirmez.
 * C# `PricingPageSmokeTests` ile aynı senaryolar (Playwright TS).
 */
test.describe('Pricing sayfası smoke @smoke', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/Pricing');
  });

  test('başlık, plan kartları ve plan seç CTA', async ({ page }) => {
    const heading = page.locator('.pricing-h1, h1').first();
    await expect(heading).toBeVisible();
    await expect(heading).toContainText(/plan/i);

    const planCards = page.locator('.pcard');
    await expect(planCards.first()).toBeVisible();
    expect(await planCards.count()).toBeGreaterThan(0);

    // Ödeme açıkken <a>...Bu Planı Seç, kapalıyken <button disabled>Ödeme yakında.
    // Ortamdan bağımsız: CTA render olsun ve iki durumdan birinin metnini taşısın.
    const buyButton = page.locator('.pcard-btn').first();
    await expect(buyButton).toBeVisible();
    await expect(buyButton).toContainText(/planı seç|ödeme yakında/i);
  });

  test('aylık / yıllık fiyat toggle çalışır', async ({ page }) => {
    const btnMonthly = page.locator('#btnMonthly');
    const btnYearly = page.locator('#btnYearly');
    await expect(btnMonthly).toBeVisible();
    await expect(btnYearly).toBeVisible();
    await expect(btnMonthly).toHaveClass(/active/);

    const starterPrice = page.locator('#amt-starter');
    const monthlyText = (await starterPrice.innerText()).trim();
    expect(monthlyText.length).toBeGreaterThan(0);

    await btnYearly.click();
    await expect(btnYearly).toHaveClass(/active/);
    await expect(starterPrice).not.toHaveText(monthlyText);
    await expect(page.locator('#ynote-starter')).not.toBeEmpty();

    await btnMonthly.click();
    await expect(btnMonthly).toHaveClass(/active/);
    await expect(starterPrice).toHaveText(monthlyText);
  });
});

test.describe('Login sayfası smoke @smoke', () => {
  test('telefon alanı ve devam butonu görünür', async ({ page }) => {
    await page.goto('/Auth/Login');
    await expect(page.locator('#phoneNumber')).toBeVisible();
    await expect(page.locator('#btnRequestOtp')).toBeVisible();
    await expect(page.locator('#btnRequestOtp')).toContainText(/devam et/i);
  });
});

/**
 * WebUI→API bağlantı smoke'u — secret/DB/OTP gerektirmez.
 * Register (GET) sektör listesini API'den (api/Sector, ApiBaseUrl HttpClient) çeker.
 * Liste boşsa WebUI API'ye ulaşamıyor demektir — "login 400 / ApiBaseUrl bozuk"
 * sınıfındaki kesintiyi OTP göndermeden yakalar.
 *
 * CI/deploy öncesi (E2E_SMOKE_PAGES_ONLY=true): API/DB yok; bu testi atla.
 * Canlı post-deploy smoke: API açık, sektör dolu olmalı.
 */
test.describe('Register WebUI→API bağlantısı @smoke', () => {
  test('Sektör dropdown API listesiyle dolu', async ({ page }) => {
    test.skip(
      process.env.E2E_SMOKE_PAGES_ONLY === 'true',
      'Sayfa smoke: API/DB yok, sektör listesi atlandı',
    );
    await page.goto('/Auth/Register');

    const sector = page.locator('#fieldSector');
    await expect(sector).toBeVisible();

    // Placeholder ("Sektör seçin...") + en az bir gerçek sektör (API'den geldi).
    const optionCount = await sector.locator('option').count();
    expect(
      optionCount,
      'Sektör listesi boş → WebUI API/Sector çağrısı başarısız (ApiBaseUrl/API bağlantısı?)',
    ).toBeGreaterThan(1);

    // Placeholder dışında değeri olan gerçek bir option bulunmalı.
    const realOptionValues = await sector.locator('option[value]:not([value=""])').count();
    expect(realOptionValues).toBeGreaterThan(0);
  });
});
