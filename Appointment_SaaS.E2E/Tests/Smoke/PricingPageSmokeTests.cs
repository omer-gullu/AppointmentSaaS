using Appointment_SaaS.E2E.Fixtures;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Appointment_SaaS.E2E.Tests.Smoke;

/// <summary>
/// Pricing sayfası duman testleri — API (5294) ve WebUI (7140) ayakta olmalı.
/// </summary>
public class PricingPageSmokeTests : E2EPageTest
{
    [Fact]
    public async Task Pricing_index_loads_with_plan_heading()
    {
        await GotoAsync("/Pricing");

        var heading = Page.Locator(".pricing-h1, h1").First;
        await Expect(heading).ToBeVisibleAsync();
        await Expect(heading).ToContainTextAsync("plan", new LocatorAssertionsToContainTextOptions { IgnoreCase = true });

        // Plan kartları var mı?
        var planCards = Page.Locator(".pcard");
        await Expect(planCards.First).ToBeVisibleAsync();
        Assert.True(await planCards.CountAsync() > 0, "En az bir plan kartı görünmeli.");

        // Satın al / plan seç CTA var mı?
        var buyButton = Page.Locator("a.pcard-btn").First;
        await Expect(buyButton).ToBeVisibleAsync();
        await Expect(buyButton).ToContainTextAsync("Planı Seç", new LocatorAssertionsToContainTextOptions { IgnoreCase = true });
    }

    [Fact]
    public async Task Pricing_page_monthly_yearly_toggle_works()
    {
        await GotoAsync("/Pricing");

        var btnMonthly = Page.Locator("#btnMonthly");
        var btnYearly = Page.Locator("#btnYearly");
        await Expect(btnMonthly).ToBeVisibleAsync();
        await Expect(btnYearly).ToBeVisibleAsync();

        await Expect(btnMonthly).ToHaveClassAsync(new Regex("active"));
        var starterMonthlyText = (await Page.Locator("#amt-starter").InnerTextAsync())?.Trim() ?? string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(starterMonthlyText));

        await btnYearly.ClickAsync();
        await Expect(btnYearly).ToHaveClassAsync(new Regex("active"));
        await Expect(Page.Locator("#amt-starter")).Not.ToHaveTextAsync(starterMonthlyText);

        var yearlyNote = Page.Locator("#ynote-starter");
        await Expect(yearlyNote).Not.ToBeEmptyAsync();

        await btnMonthly.ClickAsync();
        await Expect(btnMonthly).ToHaveClassAsync(new Regex("active"));
        await Expect(Page.Locator("#amt-starter")).ToHaveTextAsync(starterMonthlyText);
    }
}
