# Hetzner’da Evolution API kurulumu (SSH)

Bu rehber, `hetzner-evolution` SSH anahtarınızla sunucuya bağlanıp Evolution API v2’yi Docker ile kurmanız içindir.

## Ön koşullar

| Gerekli | Açıklama |
|---------|----------|
| Hetzner sunucu | Ubuntu 22.04/24.04, en az 2 GB RAM |
| SSH anahtarı | Windows: `%USERPROFILE%\.ssh\id_ed25519` (yorum: `hetzner-evolution`) |
| Sunucu IP | Hetzner Cloud → sunucu → IPv4 |
| n8n webhook URL | Evolution instance oluşturulunca buraya POST eder (genelde **https** + herkese açık) |

## 1. Hetzner’da SSH anahtarını doğrulayın

Sunucu oluştururken **SSH key** olarak şu public key eklenmiş olmalı:

```
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIKjNlFIiTZBnF5khglTdKytAWfYlO1x0iscX+trCwsoZ hetzner-evolution
```

Sunucu zaten açıksa: Hetzner Console → sunucu → **Rescue** veya yeni bir sunucu + bu key.

## 2. Windows’tan SSH bağlantısı

PowerShell:

```powershell
ssh -i $env:USERPROFILE\.ssh\id_ed25519 root@SUNUCU_IP
```

İlk girişte `Are you sure you want to continue connecting?` → `yes`.

Bağlanamazsanız:

- Hetzner firewall: **22/tcp** açık mı?
- IP doğru mu?
- `root` yerine `ubuntu` deneyin (bazı imajlar)

## 3. Sunucuda Docker kurulumu

SSH oturumunda:

```bash
apt update && apt upgrade -y
curl -fsSL https://get.docker.com | sh
docker compose version
```

## 4. Evolution dosyalarını sunucuya kopyalama

**Önemli:** Bu komut **sunucuda değil**, kendi bilgisayarınızda (Windows PowerShell) çalışır.

1. SSH oturumundaysanız önce `exit` yazın (`root@ubuntu-...` satırı kaybolmalı).
2. PowerShell’de proje köküne gidin, sonra kopyalayın:

```powershell
cd "C:\Users\pc\OneDrive\Masaüstü\AppointmentSaaS"
scp -i $env:USERPROFILE\.ssh\id_ed25519 -r deploy/evolution root@SUNUCU_IP:/opt/evolution
```

`SUNUCU_IP` yerine IPv4 yazın (ör. `116.203.121.45`). Komut satırı `PS C:\...>` ile başlamalı; `root@ubuntu-...` iken çalıştırmayın.

## 5. Ortam dosyasını düzenleme

SSH ile sunucuda:

```bash
cd /opt/evolution
cp .env.example .env
nano .env
```

Mutlaka değiştirin:

1. `AUTHENTICATION_API_KEY` — uzun rastgele (ör. `openssl rand -hex 32`)
2. `POSTGRES_PASSWORD` — güçlü şifre
3. `DATABASE_CONNECTION_URI` — aynı şifre ile `postgresql://evolution:SIFRE@postgres:5432/evolution`
4. `SERVER_URL` — `http://SUNUCU_IP:8080` veya domain (ör. `https://evolution.sizin-domain.com`)

## 6. Başlatma ve kontrol

```bash
cd /opt/evolution
docker compose up -d
docker compose ps
docker logs evolution_api --tail 80
```

Tarayıcı: `http://SUNUCU_IP:8080` — Evolution yanıt vermeli.

API anahtarı testi (sunucuda):

```bash
curl -s -H "apikey: BURAYA_AUTHENTICATION_API_KEY" http://127.0.0.1:8080/
```

## 7. Firewall (Hetzner + UFW)

Hetzner Cloud firewall: **8080** sadece sizin IP’niz veya tüm internet (test için).

```bash
ufw allow 22/tcp
ufw allow 8080/tcp
ufw enable
```

Üretimde 8080’i herkese açmak yerine Nginx + HTTPS önerilir.

## 8. AppointmentSaaS bağlantısı

`Appointment_SaaS.API` / WebUI `appsettings` veya ortam değişkenleri:

```json
"EvolutionApi": {
  "BaseUrl": "http://SUNUCU_IP:8080",
  "GlobalApiKey": "AUTHENTICATION_API_KEY ile aynı",
  "WebhookUrl": "https://N8N_PUBLIC_URL/webhook/evolution-webhook"
}
```

- **GlobalApiKey** = `.env` içindeki `AUTHENTICATION_API_KEY`
- **WebhookUrl**: n8n workflow’unuzun **public** webhook adresi (Evolution mesajları buraya gönderir)
- Yerel n8n kullanıyorsanız: ngrok/Cloudflare Tunnel ile n8n’i dışarı açın

## 9. İlk WhatsApp instance (QR)

Panel veya API:

```bash
curl -X POST "http://127.0.0.1:8080/instance/create" \
  -H "apikey: AUTHENTICATION_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"instanceName":"TENANT_INSTANCE_ADI","qrcode":true,"integration":"WHATSAPP-BAILEYS"}'
```

QR: `GET /instance/connect/TENANT_INSTANCE_ADI` veya Evolution Manager UI.

Tenant kaydı / `CreateInstanceAsync` aynı `instanceName` ile uyumlu olmalı (`E2E_INSTANCE_NAME`).

## 10. Webhook’u n8n’e bağlama

Uygulama tenant oluştururken `EvolutionApi:WebhookUrl` ile otomatik set eder. Manuel:

```bash
curl -X POST "http://127.0.0.1:8080/webhook/set/INSTANCE_ADI" \
  -H "apikey: AUTHENTICATION_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"webhook":{"enabled":true,"url":"https://N8N.../webhook/evolution-webhook","webhookByEvents":false,"events":["MESSAGES_UPSERT","CONNECTION_UPDATE"]}}'
```

## Sorun giderme

| Belirti | Çözüm |
|---------|--------|
| SSH refused | Hetzner firewall 22, doğru IP/key |
| `docker compose` yok | `apt install docker-compose-plugin` veya Docker script tekrar |
| evolution_api restart loop | `docker logs evolution_api` — genelde DB URI / şifre hatası |
| 401 apikey | Header `apikey`, GlobalApiKey eşleşmesi |
| n8n mesaj almıyor | WebhookUrl public mi? Evolution log’da webhook hatası |

## Güvenlik

- `.env` ve private key’i repoya commit etmeyin
- Üretimde HTTPS + güçlü `AUTHENTICATION_API_KEY`
- SSH sadece key ile, şifre login kapalı
