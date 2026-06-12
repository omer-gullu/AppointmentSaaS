# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: smoke.spec.ts >> Pricing sayfası smoke @smoke >> başlık, plan kartları ve plan seç CTA
- Location: tests\smoke.spec.ts:12:7

# Error details

```
Error: page.goto: net::ERR_CONNECTION_REFUSED at https://localhost:7140/Pricing
Call log:
  - navigating to "https://localhost:7140/Pricing", waiting until "load"

```

# Test source

```ts
  1  | import { test, expect } from '@playwright/test';
  2  | 
  3  | /**
  4  |  * Pricing duman testleri — DB, sqlcmd ve panel OTP gerektirmez.
  5  |  * C# `PricingPageSmokeTests` ile aynı senaryolar (Playwright TS).
  6  |  */
  7  | test.describe('Pricing sayfası smoke @smoke', () => {
  8  |   test.beforeEach(async ({ page }) => {
> 9  |     await page.goto('/Pricing');
     |                ^ Error: page.goto: net::ERR_CONNECTION_REFUSED at https://localhost:7140/Pricing
  10 |   });
  11 | 
  12 |   test('başlık, plan kartları ve plan seç CTA', async ({ page }) => {
  13 |     const heading = page.locator('.pricing-h1, h1').first();
  14 |     await expect(heading).toBeVisible();
  15 |     await expect(heading).toContainText(/plan/i);
  16 | 
  17 |     const planCards = page.locator('.pcard');
  18 |     await expect(planCards.first()).toBeVisible();
  19 |     expect(await planCards.count()).toBeGreaterThan(0);
  20 | 
  21 |     const buyButton = page.locator('a.pcard-btn').first();
  22 |     await expect(buyButton).toBeVisible();
  23 |     await expect(buyButton).toContainText(/planı seç/i);
  24 |   });
  25 | 
  26 |   test('aylık / yıllık fiyat toggle çalışır', async ({ page }) => {
  27 |     const btnMonthly = page.locator('#btnMonthly');
  28 |     const btnYearly = page.locator('#btnYearly');
  29 |     await expect(btnMonthly).toBeVisible();
  30 |     await expect(btnYearly).toBeVisible();
  31 |     await expect(btnMonthly).toHaveClass(/active/);
  32 | 
  33 |     const starterPrice = page.locator('#amt-starter');
  34 |     const monthlyText = (await starterPrice.innerText()).trim();
  35 |     expect(monthlyText.length).toBeGreaterThan(0);
  36 | 
  37 |     await btnYearly.click();
  38 |     await expect(btnYearly).toHaveClass(/active/);
  39 |     await expect(starterPrice).not.toHaveText(monthlyText);
  40 |     await expect(page.locator('#ynote-starter')).not.toBeEmpty();
  41 | 
  42 |     await btnMonthly.click();
  43 |     await expect(btnMonthly).toHaveClass(/active/);
  44 |     await expect(starterPrice).toHaveText(monthlyText);
  45 |   });
  46 | });
  47 | 
```