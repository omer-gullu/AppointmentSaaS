# AGENTS.md

## Cursor Cloud specific instructions

### Product overview

Single .NET 8 solution (`Appointment_SaaS.sln`): multi-tenant appointment SaaS (Turkish market). Runnable hosts:

| Project | URL (dev) | Role |
|---------|-----------|------|
| `Appointment_SaaS.API` | http://localhost:5294 | REST API + Swagger |
| `Appointment_SaaS.WebUI` | http://localhost:5259 | MVC dashboard (calls API) |

Libraries: `Core`, `Data`, `Business`. Tests: `Appointment_SaaS.Test` (currently **does not compile** against latest business-layer constructors — build API/WebUI only until tests are fixed).

### System dependencies (not in update script)

These are installed once on the VM image; future agents may need to **re-start** them each session:

1. **Docker daemon** (nested VM): if `docker ps` fails, start with:
   `sudo dockerd > /tmp/dockerd.log 2>&1 &` and wait a few seconds.
2. **SQL Server** (required for API/WebUI):
   ```bash
   sudo docker start appointment-sql
   ```
   If the container does not exist:
   ```bash
   sudo docker run -d --name appointment-sql \
     -e 'ACCEPT_EULA=Y' -e 'MSSQL_SA_PASSWORD=YourStrong@Passw0rd' \
     -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
   ```
3. **dotnet-ef** (migrations): `dotnet tool install --global dotnet-ef --version 8.0.23` and `export PATH="$PATH:$HOME/.dotnet/tools"`.

### Configuration

`appsettings.json` uses placeholders. For Development, set environment variables (do not commit secrets). Minimum set:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection='Server=localhost,1433;Database=AppointmentSaaS;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;'
export TokenOptions__SecurityKey='dev-jwt-secret-at-least-32-chars-long!!'
export EncryptionSettings__AesKey='0123456789abcdef0123456789abcdef'
export WebhookSecurity__N8nAuthToken='dev-webhook-token'
export EvolutionApi__BaseUrl='http://127.0.0.1:8080'
export EvolutionApi__GlobalApiKey='dev-key'
export EvolutionApi__DefaultInstance='dev-instance'
```

Optional: copy `Appointment_SaaS.API/appsettings.Development.json.template` → `appsettings.Development.json` (gitignored).

### Database migrations (important)

`dotnet ef database update` can fail on a **fresh** database because some historical migrations reference columns (`SecurityStamp`, `GoogleCalendarId`, `GoogleEmail`, `GoogleAccessToken`, `GoogleEventID`) that were never added via `AddColumn` in migration `Up()` methods — only in seed `UpdateData`.

If migrations fail with `Invalid column name 'SecurityStamp'` (or similar), either:

- Apply the workaround used in cloud setup: migrate to `20260422192936_AddAdminSeedData`, manually `ALTER TABLE` missing columns, then run full `database update`; or
- Fix/regenerate migrations in the repo (preferred long-term).

After a successful migrate, verify with `GET http://localhost:5294/api/sector` (200 + JSON).

### Running services

Start **API first**, then **WebUI** (WebUI `ApiBaseUrl` defaults to port 5294):

```bash
dotnet run --project Appointment_SaaS.API --launch-profile http
dotnet run --project Appointment_SaaS.WebUI --launch-profile http
```

Use **tmux** for long-running processes (see cloud agent shell rules).

Webhook-protected routes (e.g. `POST /api/Appointments`) require header `X-Auth-Token` matching `WebhookSecurity__N8nAuthToken`.

### Lint / test / build

| Action | Command | Notes |
|--------|---------|--------|
| Restore | `dotnet restore Appointment_SaaS.sln` | |
| Build (apps) | `dotnet build Appointment_SaaS.API/Appointment_SaaS.API.csproj` and WebUI csproj | Solution build fails on Test project |
| Format | `dotnet format Appointment_SaaS.sln` | Fails on Test project until fixed |
| Unit tests | `dotnet test Appointment_SaaS.Test/Appointment_SaaS.Test.csproj` | Compile errors as of last setup |
| Migrations | `dotnet ef database update --project Appointment_SaaS.Data --startup-project Appointment_SaaS.API --connection '<conn>'` | Pass `--connection` if placeholders remain in appsettings |

### Optional external services

- **Evolution API** (WhatsApp OTP/messaging): only needed for WhatsApp login flows.
- **n8n**: AI booking workflows; uses API webhooks + `X-Auth-Token`.
- **Google OAuth / Iyzico**: optional feature flags.

### Quick smoke test

```bash
curl -s http://localhost:5294/api/sector
curl -s -X POST http://localhost:5294/api/Appointments \
  -H 'Content-Type: application/json' -H 'X-Auth-Token: dev-webhook-token' \
  -d '{"businessPhone":"5551112233","serviceID":1,"customerName":"Test","customerPhone":"5550000001","startDate":"2026-06-15T10:00:00"}'
```

Seed tenant phone `5551112233` (Janti Erkek Kuaförü) has services in DB after migrations.
