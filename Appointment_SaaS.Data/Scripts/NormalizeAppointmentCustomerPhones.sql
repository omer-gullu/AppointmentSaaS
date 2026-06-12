-- Opsiyonel: WhatsApp LID (@s.whatsapp.net) ile kayıtlı CustomerPhone değerlerini ulusal formata çekmek.
-- Üretimde önce yedek alın; yalnızca gerçekten JID içeren satırlarda çalıştırın.
--
-- Örnek (SQL Server): @ öncesindeki rakamları alıp 90 ile başlıyorsa 0 ile başlayan 11 haneye çevir
/*
UPDATE a
SET CustomerPhone = CASE
    WHEN CHARINDEX('@', a.CustomerPhone) > 1 THEN
        CASE
            WHEN SUBSTRING(a.CustomerPhone, 1, CHARINDEX('@', a.CustomerPhone) - 1) LIKE '90%'
                 AND LEN(SUBSTRING(a.CustomerPhone, 1, CHARINDEX('@', a.CustomerPhone) - 1)) >= 12
            THEN '0' + SUBSTRING(SUBSTRING(a.CustomerPhone, 1, CHARINDEX('@', a.CustomerPhone) - 1), 3, 10)
            ELSE SUBSTRING(a.CustomerPhone, 1, CHARINDEX('@', a.CustomerPhone) - 1)
        END
    ELSE a.CustomerPhone
END
FROM Appointments a
WHERE a.CustomerPhone LIKE '%@s.whatsapp.net';
*/
