# AppointmentSaaS — Production / Staging Tam Kapsamlı Test Checklist

Ortamlar: **Staging (önerilir)** → **Production**.

---

## 0. Ön koşullar (hepsi yeşil olmadan E2E'ye geçme)

### Altyapı

- [ ] **API** publish (HTTPS, doğru domain)
- [ ] **WebUI** publish (HTTPS) — tam test için şart
- [ ] **SQL Server** (hosting): connection string, firewall, migration'lar uygulandı
  - [ ] `20260520120000_AddTenantBillingAndPendingPlan`
  - [ ] `20260520130000_AddTenantPreviousSubscriptionRef`
- [ ] **n8n** erişilebilir (HTTPS), production workflow'lar import/aktif
- [ ] **Evolution API** ayakta; API'den `BaseUrl` + `GlobalApiKey` ile erişim OK
- [ ] CORS: API `AllowedOrigins` içinde WebUI + n8n origin'leri
- [ ] Rate limit / güvenlik header'ları production'da beklenen gibi

### Konfigürasyon dosyaları

- [ ] `IyzicoSettings`: `Enabled`, sandbox **veya** canlı `ApiKey`/`SecretKey`/`BaseUrl`
- [ ] 6 plan kodu: `StarterMonthlyPlanCode` … `ProYearlyPlanCode` (panel referenceCode ile birebir)
- [ ] `WebhookSecret` (İyzico webhook imzası)
- [ ] `WebUI:PaymentCallbackUrl` veya kayıtta `PaymentCallbackUrl` → **gerçekten POST alan** URL
- [ ] `EvolutionApi`: `BaseUrl`, `GlobalApiKey`, `WebhookUrl` (n8n'e işaret)
- [ ] `Google`: ClientId/Secret, redirect URI = production WebUI
- [ ] `TokenOptions`, `EncryptionSettings`, `ConnectionStrings` — repoda secret yok, sunucuda dolu
- [ ] WebUI `ApiBaseUrl` = production API

### İyzico panel

- [ ] Sandbox veya canlı merchant'ta **abonelik** açık
- [ ] 6 pricing plan (Starter/Pro/Business × Monthly/Yearly), upgrade için **aynı product** altında gruplama
- [ ] Webhook URL: `https://{api-domain}/api/iyzico/webhook`
- [ ] Bkz. `docs/iyzico-plan-checklist.md`

### Google Cloud Console

- [ ] OAuth redirect: `https://{webui}/...` (Dashboard Google bağlantısı)
- [ ] Gerekli scope'lar (Calendar)

---

## 1. Faz A — Sadece API + DB (entegrasyon omurgası)

> WebUI yokken Postman / n8n / curl ile.

### 1.1 API sağlık

- [ ] Swagger (dev/staging) veya bilinen endpoint 401/200 davranışı
- [ ] HTTPS sertifika geçerli
- [ ] Exception middleware anlamlı JSON döndürüyor

### 1.2 Auth & tenant

- [ ] `POST /api/Auth/register-business` — Trial: `checkoutFormContent` dolu
- [ ] `POST /api/Auth/register-business` — Ücretli plan: checkout dolu, tenant **pasif** (`IsActive=false`)
- [ ] `POST /api/Auth/payment-callback` — `{ "token": "..." }` → başarı → tenant aktif
- [ ] Ödeme **yapılmadan** OTP: `POST /api/Auth/generate-otp` → **403 askıda** (beklenen)
- [ ] `POST /api/Auth/verify-otp` — aktif tenant → JWT/token
- [ ] `GET /api/Auth/session-access` — aktif/pasif doğru

### 1.3 Webhook auth (n8n)

- [ ] `GET /api/Tenants/GetContextByInstance?instanceName=...` — **X-Auth-Token yok** → 401
- [ ] Aynı istek — doğru `Tenants.ApiKey` header → 200, `IntegrationKey` dönüyor
- [ ] `GET /api/Appointments/my-active-appointments` — token + instance + phone → 200/404
- [ ] `POST /api/Appointments` — token ile randevu oluştur
- [ ] Yanlış token → 401

### 1.4 Randevu API

- [ ] `POST /api/Appointments` — oluştur
- [ ] `GET /api/Appointments/tenant/{id}`
- [ ] `PUT /api/Appointments/{id}` — güncelle
- [ ] `DELETE /api/Appointments/{id}`
- [ ] `POST /api/Appointments/lock` / `POST /api/Appointments/unlock`
- [ ] `GET /api/Appointments/available-slots`
- [ ] `GET /api/Appointments/tomorrow`
- [ ] `GET /api/Appointments/customer/{phone}`

### 1.5 Tenant / işletme API

- [ ] `GET /api/Tenants/{id}` — JWT Manager; `billingCycle`, `daysRemaining` alanları
- [ ] `POST /api/Tenants/change-plan/init` — Trial → Starter (checkout)
- [ ] Aynı döngü upgrade (Pro aylık → Business aylık) → `mode: upgrade`
- [ ] Döngü değişimi (Pro aylık → Pro yıllık) → `mode: checkout`, pending alanları DB'de
- [ ] `POST /api/Tenants/upgrade-plan` → **410** (deprecated)
- [ ] `GET /api/Tenants/integration-key` / `POST /api/Tenants/integration-key/rotate`
- [ ] `POST /api/Tenants/update-hours`, holidays CRUD
- [ ] `POST /api/Tenants/{id}/cancel-subscription`

### 1.6 Personel / servis

- [ ] `POST /api/AppUsers/add-staff` — plan limiti (Starter/Pro)
- [ ] `GET /api/AppUsers/staff/{tenantId}`
- [ ] `POST` / `PUT` / `DELETE` `/api/Services`
- [ ] `GET /api/Services/businessPhone/{phone}` — n8n

### 1.7 Google (API tarafı)

- [ ] `POST /api/Tenants/UpdateGoogleEmail` — token kaydı
- [ ] `GET /api/Tenants/GetGoogleAccessToken?instanceName=...` — geçerli refresh → access token
- [ ] Geçersiz/süresi dolmuş refresh → anlamlı hata (`invalid_grant` senaryosu)
- [ ] `POST /api/Appointments/{id}/google-event` — GoogleEventID yaz

### 1.8 WhatsApp / engelli numaralar

- [ ] `GET` / `POST` / `DELETE` `/api/WhatsAppBlockedPhones`
- [ ] `GET /api/WhatsAppBlockedPhones/check`
- [ ] `POST /api/WhatsAppBlockedPhones/opt-out`

### 1.9 İyzico webhook (API public)

- [ ] İmza **yok** → 401
- [ ] İmza **yanlış** → 401
- [ ] `payment.success` / `subscription.order.success` → tenant aktif, `TransactionLog`
- [ ] `payment.failed` — **pending plan varken** → tam askıya **yok**, pending temiz
- [ ] `payment.failed` — yenileme, pending yok → askıya
- [ ] Aynı `paymentId` iki kez → idempotent

### 1.10 Hatırlatma / audit

- [ ] `GET /api/Appointments/reminders/pending`
- [ ] `POST /api/Appointments/reminders/run` — token ile
- [ ] `POST /api/AuditLogs/workflow-error` — n8n hata logu

---

## 2. Faz B — WebUI + API (işletme ve ödeme UX)

### 2.1 Genel

- [ ] WebUI → API istekleri Bearer/cookie ile gidiyor
- [ ] Pasif tenant ile panele giriş engelli / yönlendirme

### 2.2 Kayıt & giriş

- [ ] `/Auth/Register` — Trial → İyzico form → callback → Login → OTP → Dashboard
- [ ] Register — ücretli plan → ödeme → aktif, doğru `PlanType` + `BillingCycle`
- [ ] Ödeme iptal → hesap pasif kalır
- [ ] `/Auth/Login` — OTP akışı

### 2.3 Dashboard

- [ ] Plan banner: plan/döngü, bitiş tarihi, **kalan gün**
- [ ] Trial → "Plan Seç" → `/Pricing/ChangePlan`
- [ ] Ücretli → "Planı Değiştir"
- [ ] Personel limit uyarısı (limit aşımı varsa)
- [ ] WhatsApp bağlantı durumu
- [ ] Google "Bağlı" + token yenileme
- [ ] Randevu listesi, servis/personel CRUD

### 2.4 Plan & abonelik (UI)

- [ ] `/Pricing/ChangePlan` — 6 kart, mevcut plan disabled
- [ ] Checkout modu — İyzico form embed
- [ ] Upgrade modu — anında mesaj + Dashboard'da yeni plan
- [ ] Aylık↔yıllık — ödeme sonrası plan + ref güncellenir
- [ ] Ücretli → Trial seçeneği **yok** / API reddeder

### 2.5 Instance / WhatsApp (WebUI)

- [ ] Instance oluştur / QR (`/Instance/...`)
- [ ] Evolution bağlantı sonrası bot mesajı (n8n tetiklenir)

### 2.6 Google (UI + API)

- [ ] Dashboard'dan Google OAuth bağla
- [ ] Yeni randevu → `GoogleEventID` doluyor mu
- [ ] Randevu güncelle/sil → takvim senkronu
- [ ] Token ölüyken yeniden bağlan

---

## 3. Faz C — n8n + Evolution (uçtan uca WhatsApp)

### 3.1 n8n credential

- [ ] API base URL = production
- [ ] `X-Auth-Token` = tenant `ApiKey` (integration-key ile uyumlu)
- [ ] Evolution credential doğru instance

### 3.2 Akışlar (her biri en az 1 kez)

- [ ] Workflow başlangıcı: `GetContextByInstance` — 200, bugünkü program
- [ ] Müsait slot — `available-slots`
- [ ] Randevu oluştur (WhatsApp'tan) → DB + müşteri onayı
- [ ] `my-active-appointments` — doğru tenant/telefon
- [ ] Randevu iptal/güncelleme mesajı
- [ ] Hatırlatma cron — `reminders/pending` + `reminders/run` (Pro/Business)
- [ ] Opt-out / blocked phone — bot susar
- [ ] Workflow hata → `auditlogs/workflow-error`

### 3.3 Evolution

- [ ] Instance connect/disconnect
- [ ] OTP mesajı login'de gidiyor
- [ ] Mesaj gönderimi stabil

---

## 4. Faz D — İyzico (ödeme & abonelik matrisi)

Sandbox test kartları ile; canlıda küçük tutarlı gerçek test ayrı onay.

| # | Senaryo | Beklenen |
|---|---------|----------|
| 1 | Trial kayıt + kart doğrulama + iade | Aktif trial |
| 2 | Ücretli kayıt + ilk ödeme | Aktif, gerçek `subscriptionReferenceCode` (≠ checkout token) |
| 3 | Trial → Starter aylık (ChangePlan) | Plan değişir, trial biter |
| 4 | Pro aylık → Business aylık (upgrade) | Anında plan, İyzico proration |
| 5 | Pro aylık → Pro yıllık | Eski abonelik ödeme **sonrası** iptal; pending → başarı |
| 6 | Ödeme iptal (checkout) | Pending temiz, trial/aktif korunur |
| 7 | Webhook `payment.failed` (pending varken) | Tam askıya **yok** |
| 8 | Webhook `payment.success` yenileme | `SubscriptionEndDate` güncellenir |
| 9 | `POST cancel-subscription` | İyzico + DB uyumlu |

- [ ] İyzico panelde abonelik durumu ile DB `PlanType` / `BillingCycle` eşleşiyor

---

## 5. Faz E — Güvenlik & çok kiracılı

- [ ] Manager başka `tenantId` güncelleyemez (403)
- [ ] JWT `SecurityStamp` değişince eski token ölür
- [ ] Webhook path'ler token'sız kapalı
- [ ] İyzico webhook HMAC zorunlu
- [ ] Admin-only endpoint'ler
- [ ] Kara liste / trial fingerprint (2. trial reddi)
- [ ] Refund webhook → askıya (`SuspendForRefund`)

---

## 6. Faz F — Otomasyon testleri (CI / lokal)

```bash
dotnet test Appointment_SaaS.Test/Appointment_SaaS.Test.csproj
```

- [ ] `PlanTransitionValidatorTests` — geçer
- [ ] `AuthManagerTests`, `WebhookProtectedPathEvaluatorTests` — geçer
- [ ] Bilinen kırmızı testler (AppointmentManager/Resilience) — bilinçli skip veya düzeltildi

---

## 7. Faz G — Performans & operasyon (isteğe bağlı staging)

- [ ] API cold start kabul edilebilir
- [ ] SQL yavaş sorgu / index kontrolü
- [ ] `SubscriptionExpirationWorker` süresi dolmuş tenant'ı askıya alıyor
- [ ] Loglarda secret/PII yok
- [ ] SQL yedekleme planı biliniyor

---

## 8. Yayın sırası önerisi (minimum risk)

| Sıra | Ne publish | Ne test edilir |
|------|------------|----------------|
| 1 | API + DB | Faz A + İyzico webhook |
| 2 | n8n + Evolution URL güncelle | Faz C (kısmi) |
| 3 | WebUI | Faz B + İyzico callback |
| 4 | Google redirect production | Faz B Google + Faz C takvim |
| 5 | Smoke tekrar | Tüm checklist |

**Sadece API ile tam sayılmayan:** WebUI akışları, Google OAuth UX, İyzico tarayıcı callback (WebUI veya özel callback sayfası olmadan), Instance/QR ekranları.

---

## 9. Sign-off (canlıya açmadan önce)

- [ ] Staging'de bu listeden **≥ %90** işaretlendi
- [ ] Bilinen açık sorunlar dokümante
- [ ] Rollback planı (önceki deploy + migration)
- [ ] İyzico sandbox/canlı ortamı bilinçli seçildi

---

## Hızlı referans — kritik URL'ler

| Amaç | Endpoint / sayfa |
|------|------------------|
| Kayıt | `POST /api/Auth/register-business` |
| Ödeme doğrula | `POST /api/Auth/payment-callback` veya WebUI `/Auth/PaymentCallback` |
| Plan değişimi | `POST /api/Tenants/change-plan/init` |
| n8n context | `GET /api/Tenants/GetContextByInstance` + header `X-Auth-Token` |
| İyzico webhook | `POST /api/iyzico/webhook` |
| Google token | `GET /api/Tenants/GetGoogleAccessToken` |
| Plan seçim UI | `/Pricing/ChangePlan` |

---

## İlgili dokümanlar

- `docs/iyzico-plan-checklist.md` — İyzico plan kodları ve upgrade kuralları
- `Appointment_SaaS.API/appsettings.Development.json.template` — örnek konfigürasyon
