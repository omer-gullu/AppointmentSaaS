import json
import os

file_path = "c:\\Users\\pc\\OneDrive\\Masaüstü\\otomasyon yazılımı\\KuaforAutomation (1).json"
out_path = "c:\\Users\\pc\\OneDrive\\Masaüstü\\otomasyon yazılımı\\KuaforAutomation_V2.json"

with open(file_path, 'r', encoding='utf-8') as f:
    data = json.load(f)

for node in data.get('nodes', []):
    if node.get('name') == 'AI Agent':
        system_msg = node['parameters']['options'].get('systemMessage', '')
        
        new_rules = """VERİ SETİ VE SİSTEM TEYİT KURALLARI (KRİTİK):
1. 'kendi_sistemine_kaydet' aracı için StartDate ve EndDate tarihlerini MUTLAKA 'YYYY-MM-DDTHH:mm:ss' formatında (örn: 2026-03-24T14:30:00) gönder! (1000011000 gibi YANLIŞ formatlar KULLANMA).
2. 'kendi_sistemine_kaydet' aracı başarısız olur ve "SuggestedSlots" adı altında saatler önerirse, müşteriye bu saatleri öner ve fikrini sor."""
        
        if "VERİ SETİ:" in system_msg:
            # We will just append the new rules to ensure no substring mismatch issues
            pass
        system_msg += "\n\n" + new_rules
        node['parameters']['options']['systemMessage'] = system_msg

    if node.get('name') == 'kendi_sistemine_kaydet':
        node['parameters']['bodyParameters'] = {
            "parameters": [
                {
                    "name": "CustomerName",
                    "value": "={{ /*n8n-auto-generated-fromAI-override*/ $fromAI('CustomerName', `Müşterinin Adı`, 'string') }}"
                },
                {
                    "name": "CustomerPhone",
                    "value": "={{ /*n8n-auto-generated-fromAI-override*/ $fromAI('CustomerPhone', `Müşterinin telefon numarası (örn: 5078283441)`, 'string') }}"
                },
                {
                    "name": "BusinessPhone",
                    "value": "5078283441"
                },
                {
                    "name": "ServiceID",
                    "value": "1"
                },
                {
                    "name": "AppUserID",
                    "value": ""
                },
                {
                    "name": "StartDate",
                    "value": "={{ /*n8n-auto-generated-fromAI-override*/ $fromAI('StartDate', `Başlangıç Tarihi YYYY-MM-DDTHH:mm:ss`, 'string') }}"
                },
                {
                    "name": "EndDate",
                    "value": "={{ /*n8n-auto-generated-fromAI-override*/ $fromAI('EndDate', `Bitiş Tarihi YYYY-MM-DDTHH:mm:ss`, 'string') }}"
                }
            ]
        }

with open(out_path, 'w', encoding='utf-8') as f:
    json.dump(data, f, ensure_ascii=False, indent=2)

print(f"Updated JSON saved to {out_path}")
