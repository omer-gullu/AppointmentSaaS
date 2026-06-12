# Appointment SaaS — Playwright E2E

Tarayıcı tabanlı uçtan uca testler (`Microsoft.Playwright` + xUnit).

## Önkoşul

1. **API** çalışıyor: `http://localhost:5294`
2. **WebUI** çalışıyor: `https://localhost:7140` (veya `appsettings.json` / ortam değişkenindeki URL)
3. Playwright tarayıcıları yüklü

## İlk kurulum

```powershell
cd Appointment_SaaS.E2E
dotnet build
# pwsh yoksa PowerShell ile:
powershell -ExecutionPolicy Bypass -File bin\Debug\net8.0\playwright.ps1 install chromium
```

## Test çalıştırma

```powershell
cd Appointment_SaaS.E2E
dotnet test
```

Tek test:

```powershell
dotnet test --filter "FullyQualifiedName~PricingPageSmokeTests"
```

## Yapılandırma

| Kaynak | Örnek |
|--------|--------|
| `appsettings.json` → `E2E:WebUiBaseUrl` | `https://localhost:7140` |
| Ortam değişkeni `PLAYWRIGHT_BASE_URL` | WebUI kök URL (öncelikli) |
| `E2E:ApiBaseUrl` | API adresi (ileride API doğrudan testleri için) |
| `E2E:IgnoreHttpsErrors` | `true` (yerel dev sertifikası) |

## Yeni test ekleme

1. `Tests/` altında klasör açın (ör. `Tests/Auth/`).
2. `E2EPageTest` sınıfından türetin.
3. `await GotoAsync("/yol");` ve `await Expect(...)` kullanın.

Örnek: `Tests/Smoke/PricingPageSmokeTests.cs`

## Notlar

- Mevcut **Appointment_SaaS.Test** (xUnit + Moq) birim/entegrasyon testleridir; Playwright ile çakışmaz.
- E2E testleri gerçek çalışan uygulamaya istek atar; CI'da API + WebUI'yi önce ayağa kaldırın veya pipeline'a ekleyin.
