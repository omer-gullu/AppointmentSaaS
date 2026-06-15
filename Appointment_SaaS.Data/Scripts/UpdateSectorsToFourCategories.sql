-- Sektör listesini 4 kategoriye günceller (Erkek/Kadın/Unisex Kuaför kaldırılır).
-- Migration uygulanamazsa sunucuda manuel çalıştırılabilir.

UPDATE "Tenants"
SET "SectorID" = 1
WHERE "SectorID" IN (1, 2, 3)
  AND "TenantID" <> 2;

UPDATE "Sectors"
SET "Name" = 'Kuaför',
    "DefaultPrompt" = 'Sen profesyonel bir kuaför randevu asistanısın. Saç kesimi, boya, bakım ve randevu konularında net, nazik ve çözüm odaklı konuş.'
WHERE "SectorID" = 1;

UPDATE "Sectors"
SET "Name" = 'Güzellik Salonu',
    "DefaultPrompt" = 'Sen güzellik salonu randevu asistanısın. Cilt bakımı, manikür, epilasyon ve bakım hizmetlerinde detaycı, nazik ve profesyonel konuş.'
WHERE "SectorID" = 2;

UPDATE "Sectors"
SET "Name" = 'Diş Kliniği',
    "DefaultPrompt" = 'Sen diş kliniği randevu asistanısın. Muayene, tedavi ve kontrol randevularında güven veren, açık ve profesyonel bir dille konuş.'
WHERE "SectorID" = 3;

INSERT INTO "Sectors" ("SectorID", "CreatedAt", "DefaultPrompt", "Name")
SELECT 4, TIMESTAMPTZ '2026-01-01 00:00:00+00',
       'Sen psikolog randevu asistanısın. Görüşme randevularında empatik, saygılı, gizliliğe özen gösteren ve sakin bir dille konuş.',
       'Psikolog'
WHERE NOT EXISTS (SELECT 1 FROM "Sectors" WHERE "SectorID" = 4);

UPDATE "Tenants" SET "SectorID" = 2 WHERE "TenantID" = 2;
UPDATE "Tenants" SET "SectorID" = 1 WHERE "TenantID" = 3;

SELECT "SectorID", "Name" FROM "Sectors" ORDER BY "SectorID";
