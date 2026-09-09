# Nginx güvenlik sıkılaştırması — AppointmentSaaS

Bu klasör Hetzner sunucusundaki nginx için **şablon** içerir. Dosyaları doğrudan kopyalayıp
sunucudaki SSL path / port değerlerini doğrulayın.

## Ne düzeltilir?

| ZAP / denetim bulgusu | Nginx karşılığı |
|----------------------|-----------------|
| TRACE / Proxy Disclosure | `TRACE\|TRACK` → 405 |
| Çift HSTS | HSTS **yalnızca** nginx'te; uygulama `UseHsts` kaldırıldı |
| Server fingerprint | `server_tokens off` |
| Rate-limit IP spoof | `X-Real-IP` / `X-Forwarded-For` = `$remote_addr` (istemci başlığı ezilir) |

## Kurulum (sunucuda, bir kez)

```bash
# 1) Snippet
sudo mkdir -p /etc/nginx/snippets
sudo cp deploy/nginx/snippets/security-headers.conf /etc/nginx/snippets/

# headers-more-nginx-module yoksa security-headers.conf içindeki
# "more_clear_headers Server;" satırını silin (opsiyonel; server_tokens off yeterli).

# 2) Site
sudo cp deploy/nginx/sites-available/akillirandevu.net.conf \
        /etc/nginx/sites-available/akillirandevu.net
sudo ln -sf /etc/nginx/sites-available/akillirandevu.net /etc/nginx/sites-enabled/

# API'yi internete AÇMAYIN mümkünse. Zorunluysa:
# sudo cp deploy/nginx/sites-available/api.akillirandevu.net.conf \
#         /etc/nginx/sites-available/api.akillirandevu.net
# sudo ln -sf ... /etc/nginx/sites-enabled/

# 3) Test + reload
sudo nginx -t && sudo systemctl reload nginx
```

## WebUI port

Şablonda WebUI `127.0.0.1:5259`. Canlıda Kestrel farklı porta bind ediyorsa `proxy_pass` satırını güncelleyin
(`/opt/appointmentsaas/webui.env` / systemd unit).

## API public mi?

Önerilen mimari:

- WebUI → `http://127.0.0.1:5294` (sunucu içi)
- n8n → `http://127.0.0.1:5294` (sunucu içi)
- İnternetten `/api` **yok**

Bu durumda `api.akillirandevu.net` vhost'unu **kurmayın**. Trial JWT ile API'ye dışarıdan
erişim yüzeyi kapanır (önceki kritik reminders açığı için savunma derinliği).

## Doğrulama

```bash
# TRACE kapalı
curl -X TRACE -i https://akillirandevu.net/ | head

# Tek HSTS
curl -sI https://akillirandevu.net/ | grep -i strict-transport

# X-Real-IP spoof işe yaramamalı (rate-limit aynı IP kovası)
# (OTP generate-otp ile 4. istekte 429 beklenir)
```
