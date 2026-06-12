# n8n Evolution API fix (güncel workflow)

Bu düzeltme `Send via Evolution API` node'undaki `Invalid URL` ve `apikey undefined` hatalarını giderir.

## 1. Map Variables — 2 alan ekle + `.first()` kullan

Mevcut Evolution Webhook atamalarında `.item` → `.first()` yapın.

**Yeni alanlar:**

| Name | Value (Expression) |
|------|-------------------|
| `EvolutionServerUrl` | `{{ $('Evolution Webhook').first().json.body.server_url \|\| $('Evolution Webhook').first().json.server_url }}` |
| `EvolutionApiKey` | `{{ $('Evolution Webhook').first().json.body.apikey \|\| $('Evolution Webhook').first().json.apikey }}` |

## 2. Send via Evolution API

- **URL:** `={{ $('Map Variables').first().json.EvolutionServerUrl }}/message/sendText/{{ $('Map Variables').first().json.BusinessInstance }}`
- **Header apikey:** `={{ $('Map Variables').first().json.EvolutionApiKey }}`
- **Body text:** `={{ $('AI Agent').first().json.output }}`
- **Settings → Execute Once:** ON

## 3. AI tool header'ları (önerilir)

`kendi_sistemine_kaydet`, `takvimi_oku`, `randevu_sil` içinde:

`$('Get Mega Context').item.json` → `$('Get Mega Context').first().json`

## 4. takvimi_oku

Google Calendar kalıntısı **queryParameters** (timeMin, timeMax…) silin — URL zaten `available-slots` API'nizi kullanıyor.

## Import

`n8n-kuafor-automation-current-fixed.json` dosyasını n8n → Import from File ile yükleyin.
