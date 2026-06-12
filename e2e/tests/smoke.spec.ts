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

    const buyButton = page.locator('a.pcard-btn').first();
    await expect(buyButton).toBeVisible();
    await expect(buyButton).toContainText(/planı seç/i);
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
