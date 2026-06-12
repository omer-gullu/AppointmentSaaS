# Playwright E2E (TypeScript)

Tarayıcı ve API tabanlı uçtan uca testler. Mevcut `Appointment_SaaS.E2E` (C#) projesinden bağımsızdır.

## Test dosyaları

| Dosya | Kapsam |
|-------|--------|
| `tests/appointment.spec.ts` | Panel (opsiyonel) + API randevu POST |
| `tests/n8n-contract.spec.ts` | n8n’in kullandığı API uçları (`GetContext`, `available-slots`, gri liste, hatırlatma) — **n8n/Gemini gerekmez** |
| `tests/n8n-workflow.spec.ts` | Canlı n8n Evolution webhook (`E2E_N8N_BASE_URL`); **“asistanı kapat”** → gri liste (AI yok) |
| `tests/n8n-gemini.spec.ts` | Canlı n8n + **Gemini** sohbet → `kendi_sistemine_kaydet` → DB (`E2E_RUN_GEMINI=true`, ücretli) |
| `tests/n8n-ai-edge-cases.spec.ts` | AI kenar durumları: belirsiz zaman, çakışma, spam, paralel mesaj (`E2E_RUN_AI_EDGE=true`) |
| `tests/appointment-mutations.spec.ts` | Randevu **güncelle/sil** (API + panel): tarih, hizmet, personel |
| `tests/n8n-behavior.spec.ts` | n8n: opt-out, gri liste, rate limit, context; failover/özür (manuel flag) |
| `tests/payment.spec.ts` | İmzalı Iyzico webhook, tenant `IsActive`, ödeme sonrası panel |
| `tests/billing.spec.ts` | Webhook `payment.failed` (askı / pending plan), panel `ChangePlan` |
| `tests/security.spec.ts` | Çapraz-kiracı API 403 + pasif tenant panel redirect |
| `tests/register-checkout.spec.ts` | WebUI kayıt + İyzico webhook aktivasyon + OTP dashboard |
| `tests/smoke.spec.ts` | Pricing duman: plan kartları, CTA, aylık/yıllık toggle — DB/OTP yok |

## Ortamlar (`playwright.config.ts`)

| `PLAYWRIGHT_ENV` | Davranış |
|------------------|----------|
| `development` | Tüm testler (varsayılan) |
| `staging` | `E2E_WEB_UI_URL` / `E2E_API_URL` zorunlu |
| `production` | Sadece `@smoke`, `@destructive` hariç (readonly) |

## Kurulum

```powershell
cd e2e
npm install
npx playwright install chromium
copy .env.example .env
cd scripts
.\discover-env.ps1
# çıktıyı .env içine yapıştır (E2E_TENANT_ID, INSTANCE, N8N_TOKEN, …)
```

**Statik yapılandırma:** Tüm API/n8n testleri `.env` değerlerini kullanır (`helpers/e2e-config.ts`). OTP testi kaldırıldı. Panel testleri varsayılan **kapalı** (`E2E_RUN_PANEL_TESTS=false`).

**Önkoşul:** API + WebUI ayakta; SQL Server (DB assert için).

## Çalıştırma

```powershell
# Tüm testler (development)
npm test

# Pricing smoke (DB/sqlcmd/OTP yok; WebUI ayakta olmalı)
npm run test:smoke

# Ortam seçimi
npm run test:staging
npm run test:production

# API odaklı (panel/OTP yok)
npm run test:api

# n8n API sözleşmesi (n8n sunucusu şart değil)
npm run test:n8n:contract

# Canlı n8n webhook (workflow Active + E2E_N8N_BASE_URL)
npm run test:n8n:live

# Gemini sohbet (ücretli; n8n + API ayakta)
npm run test:n8n:gemini

# Randevu güncelle / sil (panel + API)
npm run test:mutations

# Abonelik webhook + ChangePlan paneli
npm run test:billing
# Gerekir: e2e/.env → IYZICO_WEBHOOK_SECRET, E2E_TENANT_ID, E2E_DB_*; panel için E2E_RUN_PANEL_TESTS=true

# İyzico başarı webhook + panel giriş
npm run test:payment

# n8n davranış (opt-out, gri liste, rate limit, …)
npm run test:n8n:behavior

# Güvenlik (çapraz tenant — iki tenant env gerekli)
npm run test:security

# Kayıt + ödeme (hibrit, İyzico açık olmalı)
npm run test:register
```

## K6 yük testleri (CANLI)

[k6](https://k6.io/docs/get-started/installation/) CLI sistemde kurulu olmalı (npm paketi değil).

```powershell
cd e2e\scripts
.\discover-env.ps1 -ExportLoadTenants
# fixtures/load-tenants.json üretilir (gitignore)

cd ..
# .env içine: E2E_LOAD_TENANTS_JSON=fixtures/load-tenants.json
# webhook/AI için: E2E_N8N_WEBHOOK_URL, E2E_EVOLUTION_BASE_URL

npm run test:load:appointments
npm run test:load:webhook
npm run test:load:webhook:light   # 10 VU — n8n hazır mı kontrol
npm run test:load:race
npm run test:load:ai   # E2E_RUN_LOAD_AI=true — Gemini maliyeti
```

| Script | Açıklama |
|--------|----------|
| `load-tests/appointment-load.js` | 100 tenant paralel randevu POST |
| `load-tests/n8n-webhook-load.js` | n8n Evolution webhook flood (`E2E_LOAD_VUS`, setup’ta `/healthz`) |

**n8n yük testi 503 (`Database is not ready!`):** Bu hata AppointmentSaaS API’sinden değil, **yerel n8n** SQLite/Postgres bağlantısından gelir. 100 VU ile n8n DB kilitlenir. Önce `E2E_N8N_BASE_URL=http://localhost:5678` ile n8n’in UI’da açıldığını doğrulayın, workflow **Active** olsun, sonra `npm run test:load:webhook:light`. Tam yük için `E2E_LOAD_VUS=20` ile kademeli artırın. Atlamak için: `K6_SKIP_N8N_READY=true` (önerilmez).

**Süre eşiği (ERRO thresholds):** Webhook Gemini/AI workflow’u senkron ~15–45s sürebilir; varsayılan k6 eşikleri `p95<45s`, `fail<5%`. Saf HTTP SLA ölçmek için: `E2E_LOAD_STRICT_THRESHOLDS=true`.
| `load-tests/whatsapp-webhook-load.js` | 100 instance n8n webhook + 10 VU slot race |
| `load-tests/ai-response-load.js` | AI yanıt yükü (varsayılan kapalı flag) |

**Veri tipleri:** STATİK (`.env` tenant listesi) + DİNAMİK (K6 içinde telefon/slot) + SANDBOX (`5320000xxx`) + CANLI (n8n/Gemini).

### n8n davranış testleri (`n8n-behavior`)

| Senaryo | Otomatik? | Not |
|---------|-----------|-----|
| `asistanı kapat` / `Asistanı kapat` | Evet | Gri listeye eklenir |
| Gri listede yanıt / randevu yok | Evet | 15 sn içinde yeni DB randevusu yok |
| Redis rate limit | Evet | Varsayılan 16 ardışık webhook, 500 olmamalı |
| Hafızamı tazele | Kısmi | Webhook + `GetContext` dolu (node adı workflow’a bağlı) |
| 1. agent → 2. agent | Manuel | `E2E_N8N_VERIFY_AI_FAILURE=true` + n8n’de 1. agent’ı hata verecek test modu |
| Özür mesajı | Manuel | Aynı flag; Evolution gönderimi log’dan doğrulanır |

İkinci hizmet/personel için tenant’ta en az 2 hizmet ve 2 personel olmalı (`appointment-mutations` alternatif ID testi).

## n8n ve Gemini

| Test | n8n çalışır mı? | Gemini gerekir mi? |
|------|-----------------|-------------------|
| `n8n-contract` | Hayır | Hayır |
| `appointment` API POST | Hayır | Hayır |
| `n8n-workflow` webhook | Evet | Hayır (`asistanı kapat` dalı) |
| `n8n-gemini` | Evet | Evet |
| Gerçek WhatsApp (müşteri cebi) | Evet | Evet |

`E2E_N8N_BASE_URL=http://localhost:5678` (veya `E2E_N8N_WEBHOOK_URL`) ve workflow **Active** olmalı.

### Gemini E2E

- Komut: `npm run test:n8n:gemini` (`E2E_RUN_GEMINI=true`)
- 3 mesaj → n8n Evolution webhook → AI Agent → `kendi_sistemine_kaydet` → DB poll
- Müşteri: `5320000xxx` (sandbox)
- Varsayılan bekleme: mesajlar arası 30 sn, DB poll 6 dk, test timeout 10 dk

## İşletme ayarları (randevu testleri öncesi)

| Ayar | Üründe | E2E |
|------|--------|-----|
| **Çalışma saatleri** | Panel → Dashboard → *Çalışma Saatleri* veya `POST /api/Tenants/update-hours` | `test:appointment` başında DB’de otomatik: Pazar kapalı, diğer günler **08:00–21:00** |
| **Mola saatleri** | Dashboard → *Mola Saatleri* (varsayılan 12:00–13:00) | Randevu testleri tenant’ta `ensureE2eBreakTime` ile 12:00–13:00 ayarlar |
| **Resmi tatiller** | *Tatil Yönetimi* veya kayıt sırasında seed | Oluşturma/güncelleme API + panel tatil gününde reddeder |

Gerçek işletmede panelden saatleri de ayarlayın; E2E yalnızca test tenant’ını (`E2E_TENANT_ID`) günceller.

## Gerekli ortam değişkenleri

- **Her zaman:** `E2E_WEB_UI_URL`, `E2E_API_URL` (development’ta varsayılan localhost)
- **OTP / panel:** `E2E_MANAGER_PHONE` (+ `E2E_DATABASE_URL` veya `E2E_OTP_CODE`)
- **Randevu:** `E2E_TENANT_ID`, `E2E_SERVICE_ID`, `E2E_STAFF_ID`, `E2E_INSTANCE_NAME`, `E2E_N8N_TOKEN`, `E2E_DATABASE_URL`
- **Ödeme webhook:** `IYZICO_WEBHOOK_SECRET`, `E2E_DATABASE_URL`, tenant `SubscriptionReferenceCode`
- **Güvenlik:** `E2E_TENANT_A_*`, `E2E_TENANT_B_*`, `E2E_PASSIVE_*` (bkz. `.env.example`)
- **K6:** `E2E_LOAD_TENANTS_JSON` (dosya veya inline JSON, `discover-env.ps1 -ExportLoadTenants`)

## CI

`.github/workflows/playwright-e2e.yml` — secret’lar tanımlıysa tam suite, değilse smoke.
