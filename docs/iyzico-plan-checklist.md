# İyzico Abonelik Plan Checklist

Merchant panelinde **6 ücretli pricing plan** tanımlı olmalı ve `appsettings` / `appsettings.Development.json` içindeki `IyzicoSettings` ref kodlarıyla birebir eşleşmelidir.

## Pricing plan referans kodları

| Plan     | Döngü  | appsettings anahtarı (örnek)   |
|----------|--------|--------------------------------|
| Starter  | Aylık  | `StarterMonthlyPricingPlanReferenceCode` |
| Starter  | Yıllık | `StarterYearlyPricingPlanReferenceCode`  |
| Pro      | Aylık  | `ProMonthlyPricingPlanReferenceCode`     |
| Pro      | Yıllık | `ProYearlyPricingPlanReferenceCode`      |
| Business | Aylık  | `BusinessMonthlyPricingPlanReferenceCode`|
| Business | Yıllık | `BusinessYearlyPricingPlanReferenceCode` |

Şablon: `Appointment_SaaS.API/appsettings.Development.json.template`

## Ürün gruplama (Upgrade API)

İyzico **Upgrade Subscription** yalnızca **aynı product + aynı paymentInterval** için çalışır:

- Pro aylık → Business aylık: `POST .../subscriptions/{ref}/upgrade`
- Pro aylık → Pro yıllık: **Cancel** + yeni **Checkout Form** (uygulama böyle yapar)

Panelde Starter/Pro/Business planlarını aynı product altında, aylık ve yıllık olarak ayrı pricing plan olarak tanımlayın.

## Webhook

- `WebhookSecret` appsettings'te dolu olmalı
- URL: `POST /api/iyzico/webhook`
- İşlenen eventler: `payment.success`, `subscription.order.success`, `payment.failed`, `subscription.order.failure`, `subscription.upgraded`, iptal/iade

## Doğrulama

1. Trial kayıt → kart doğrulama → hesap aktif
2. Trial → Starter aylık checkout → `subscriptionReferenceCode` DB'de gerçek ref olmalı (checkout token değil)
3. Pro aylık → Business aylık: upgrade, bitiş tarihi detail sync
4. Pro aylık → Pro yıllık: cancel + checkout, ödeme öncesi eski plan geçerli
5. `payment.failed` → tenant askıda, pending plan temiz
