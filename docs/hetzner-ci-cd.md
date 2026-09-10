# Hetzner CI/CD — tüm solution katmanları

## Solution projeleri (7) ve CI/CD rolü

| Proje | CI | Deploy |
|-------|-----|--------|
| **Appointment_SaaS.Core** | `ci.yml` build | API/WebUI DLL içinde |
| **Appointment_SaaS.Data** | `ci.yml` build | API/WebUI DLL içinde |
| **Appointment_SaaS.Business** | `ci.yml` build | API/WebUI DLL içinde |
| **Appointment_SaaS.API** | build + test | `deploy-hetzner.yml` → Hetzner |
| **Appointment_SaaS.WebUI** | build + test | `deploy-hetzner.yml` → Hetzner |
| **Appointment_SaaS.Test** | `dotnet test` | Deploy edilmez |
| **Appointment_SaaS.E2E** | derleme kontrolü | Deploy edilmez |

**Solution dışı:** `e2e/` (Node Playwright) → `playwright-e2e.yml`  
**Altyapı:** `deploy/n8n`, `deploy/evolution` → `deploy-hetzner-infra.yml`

---

## Workflow dosyaları

| Dosya | Tetikleyici | Ne yapar |
|-------|-------------|----------|
| **ci.yml** | Her push / PR | Solution build + unit test + E2E derleme |
| **deploy-hetzner.yml** | Manuel (`workflow_dispatch`) veya `main` push | Validate (**unit test zorunlu**) → API ve/veya WebUI rsync. Test fail → deploy yok. |
| **deploy-hetzner-infra.yml** | Manuel | n8n / Evolution docker sync (`.env` hariç) |
| **playwright-e2e.yml** | e2e / API / WebUI değişince | Canlı E2E (secret gerekir) |

> Deploy workflow'larında `push` tetikleyicisi şimdilik kapalı. GitHub Secrets hazır olunca `deploy-hetzner.yml` içindeki yorum satırlarını açın.

---

## GitHub Secrets

| Secret | Zorunlu |
|--------|---------|
| `HETZNER_SSH_PRIVATE_KEY` | Deploy için evet |
| `HETZNER_HOST` | Deploy için evet |
| `HETZNER_USER` | Hayır (varsayılan `root`) |

**Playwright E2E (staging/production):** `E2E_DATABASE_URL`, `E2E_N8N_TOKEN`, `E2E_WEB_UI_URL`, vb.

---

## Sunucu dizinleri

```
/opt/appointmentsaas/api/
/opt/appointmentsaas/webui/
/opt/appointmentsaas/api.env
/opt/appointmentsaas/webui.env
/opt/n8n/
/opt/evolution/
/opt/postgres/          # deploy/postgres/docker-compose.yml
```

Infra deploy **`.env` dosyalarını üzerine yazmaz**.

---

## Manuel deploy (PowerShell)

```powershell
.\scripts\deploy-api.ps1
.\scripts\deploy-webui.ps1
```

---

## İlk kurulum (sunucu, bir kez)

1. .NET 8 runtime
2. **PostgreSQL** (Docker, yalnızca localhost):
   ```bash
   mkdir -p /opt/postgres && cd /opt/postgres
   # deploy/postgres/docker-compose.yml ve .env (sunucuda oluşturun, repoya commit etmeyin)
   docker compose up -d
   ```
3. `api.env` / `webui.env` — şablonlar: `deploy/api/env.example`, `deploy/webui/env.example`.
   Deploy dosya yoksa şablondan oluşturur; mevcut sırları **ezmez**.
   WebUI `ApiBaseUrl` her deploy'da `http://127.0.0.1:5294` olarak sabitlenir (public hostname login'i bozar).
4. Veritabanı şeması (ilk deploy sonrası veya deploy öncesi):
   ```bash
   cd /opt/appointmentsaas/api
   dotnet Appointment_SaaS.API.dll ef database update
   # veya geliştirme makinesinden:
   dotnet ef database update --project Appointment_SaaS.Data --startup-project Appointment_SaaS.API
   ```
5. Nginx + certbot: `akillirandevu.net` (WebUI). Şablon: `deploy/nginx/` (HSTS tek kaynak, TRACE kapalı, X-Real-IP = `$remote_addr`).
   - API'yi mümkünse internete **açmayın** (WebUI/n8n → `127.0.0.1:5294`). Ayrıntı: `deploy/nginx/README.md`.
6. GitHub secrets
7. Actions → Deploy to Hetzner → Run workflow

> **Not:** n8n kendi Postgres instance'ını kullanır (`deploy/n8n`). Uygulama DB'si ayrıdır (`deploy/postgres`). Port 5432 dışarıya açılmamalı (`127.0.0.1:5432` bind).

---

## Akış özeti

```
PR / push → ci.yml: sln build + Test + E2E compile

Deploy (push main veya manuel)
  → validate: build + Appointment_SaaS.Test
  → test fail ise rsync/systemctl çalışmaz
  → test geçerse API/WebUI rsync + restart

Manuel infra deploy
  → deploy-hetzner-infra: rsync compose → docker compose up
```

Canlı Playwright (`playwright-e2e.yml`) OTP / tünel / n8n’e bağlıdır; **deploy’u kilitlemez**. Kapı: `Appointment_SaaS.Test` (206 unit).

n8n **workflow JSON** repoda olsa bile CI/CD onları sunucuya otomatik yüklemez — import UI'dan yapılır.
