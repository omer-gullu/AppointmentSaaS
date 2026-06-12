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
| **deploy-hetzner.yml** | Manuel (`workflow_dispatch`) | Validate → API ve/veya WebUI rsync |
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
2. SQL Server (Docker)
3. `api.env` / `webui.env` — şablonlar: `deploy/api/`, `deploy/webui/`
4. Nginx + certbot: `api.`, `app.`, `n8n.` subdomain (api/app sonra da eklenebilir)
5. GitHub secrets
6. Actions → Deploy to Hetzner → Run workflow

---

## Akış özeti

```
PR / push → ci.yml: sln build + Test + E2E compile

Manuel deploy (secrets hazır)
  → deploy-hetzner: validate → rsync → systemctl restart

Manuel infra deploy
  → deploy-hetzner-infra: rsync compose → docker compose up
```

n8n **workflow JSON** repoda olsa bile CI/CD onları sunucuya otomatik yüklemez — import UI'dan yapılır.
