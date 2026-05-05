$filePath = "c:\Users\pc\OneDrive\Masaüstü\otomasyon yazılımı\KuaforAutomation (1).json"
$outPath = "c:\Users\pc\OneDrive\Masaüstü\otomasyon yazılımı\KuaforAutomation_Fixed.json"

$jsonText = Get-Content $filePath -Raw | ConvertFrom-Json

foreach ($node in $jsonText.nodes) {
    if ($node.name -eq "AI Agent") {
        $sysMsg = $node.parameters.options.systemMessage
        $newRules = "

SİSTEM TEYİT KURALLARI (KRİTİK):
1. 'kendi_sistemine_kaydet' aracı için StartDate ve EndDate tarihlerini MUTLAKA 'YYYY-MM-DDTHH:mm:ss' formatında (örn: 2026-03-24T14:30:00) gönder!
2. 'kendi_sistemine_kaydet' aracı başarısız olur ve 'SuggestedSlots' listesi dönerse, müşteriye 'Maalesef o saat dolu ama şu saatler boş: ... Hangisi uyar?' şeklinde otomatik öneride bulun."
        $node.parameters.options.systemMessage = $sysMsg + $newRules
    }

    if ($node.name -eq "kendi_sistemine_kaydet") {
        $node.parameters | Add-Member -MemberType NoteProperty -Name "descriptionType" -Value "manual" -Force
        $node.parameters | Add-Member -MemberType NoteProperty -Name "toolDescription" -Value "Randevuyu veritabanına kaydetmek için çağırılır. Başarısız olursa döneceği alternatif slotları (SuggestedSlots) okuyup müşteriye söylemelisin." -Force

        $newParams = @(
            @{ name = "CustomerName"; value = "={{ /*n8n-auto-generated-fromAI-override*/ `$fromAI('CustomerName', ``Müşterinin Adı``, 'string') }}" },
            @{ name = "CustomerPhone"; value = "={{ /*n8n-auto-generated-fromAI-override*/ `$fromAI('CustomerPhone', ``Müşterinin telefon numarası (örn: 5078283441)``, 'string') }}" },
            @{ name = "BusinessPhone"; value = "5078283441" },
            @{ name = "ServiceID"; value = "1" },
            @{ name = "AppUserID"; value = "" },
            @{ name = "StartDate"; value = "={{ /*n8n-auto-generated-fromAI-override*/ `$fromAI('StartDate', ``Başlangıç Tarihi YYYY-MM-DDTHH:mm:ss``, 'string') }}" },
            @{ name = "EndDate"; value = "={{ /*n8n-auto-generated-fromAI-override*/ `$fromAI('EndDate', ``Bitiş Tarihi YYYY-MM-DDTHH:mm:ss``, 'string') }}" }
        )
        
        $node.parameters.bodyParameters.parameters = $newParams
    }
}

$jsonText | ConvertTo-Json -Depth 10 | Set-Content $outPath -Encoding UTF8
Write-Host "JSON Updated at: $outPath"
