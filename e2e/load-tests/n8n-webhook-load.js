/**
 * n8n Evolution webhook yük testi (Evolution API'ye doğrudan istek YOK).
 *
 * Gerekli ortam değişkenleri:
 *   N8N_WEBHOOK_URL      — n8n webhook tam URL (ör. https://n8n.example/webhook/evolution-webhook)
 *   N8N_AUTH_TOKEN       — Tenant X-Auth-Token (API ile aynı; isteğe bağlı header)
 *   E2E_INSTANCE_NAME    — Evolution instance / tenant instance adı
 *   E2E_TENANT_ID        — Tenant ID (header X-Tenant-Id)
 *
 * Uyumluluk (opsiyonel takma adlar):
 *   E2E_N8N_WEBHOOK_URL, E2E_N8N_TOKEN
 *
 * Senaryo seçimi:
 *   SCENARIO=load  (varsayılan) — 100 sanal kullanıcı webhook flood
 *   SCENARIO=race               — 10 paralel aynı slot randevu denemesi
 *
 * Opsiyonel:
 *   E2E_EVOLUTION_BASE_URL — payload server_url (n8n workflow için; Evolution'a POST edilmez)
 *   E2E_RACE_SLOT_MESSAGE  — race senaryosunda gönderilecek sabit mesaj
 *
 * Çalıştırma (e2e klasöründen):
 *   npm run test:load:webhook
 *   npm run test:load:race
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

// k6 v2: tenants.js k6/fs kullanır — bu dosyada inline (sadece http modülü).
function buildEvolutionPayload(instance, customerPhone, messageText) {
  let digits = customerPhone.replace(/\D/g, '');
  if (digits.startsWith('0')) digits = `90${digits.slice(1)}`;
  if (digits.length === 10) digits = `90${digits}`;
  const jid = `${digits}@s.whatsapp.net`;
  const serverUrl = (__ENV.E2E_EVOLUTION_BASE_URL || 'http://localhost:8080').trim();
  const apikey = (__ENV.E2E_EVOLUTION_APIKEY || 'e2e').trim();
  return {
    event: 'messages.upsert',
    instance,
    server_url: serverUrl,
    apikey,
    data: {
      key: { remoteJid: jid, fromMe: false },
      sender: digits,
      pushName: 'K6 Load',
      message: { conversation: messageText },
    },
  };
}

function futureSlotIso(daysAhead, hour, minute) {
  const d = new Date();
  d.setDate(d.getDate() + daysAhead);
  while (d.getDay() === 0) d.setDate(d.getDate() + 1);
  d.setHours(hour, minute, 0, 0);
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
}

// ─── Ortam ───────────────────────────────────────────────────────────────
/** Playwright helpers/n8n.ts ile aynı mantık. */
function resolveN8nWebhookUrl() {
  const full = (__ENV.N8N_WEBHOOK_URL || __ENV.E2E_N8N_WEBHOOK_URL || '').trim();
  if (full) return full;
  const base = (__ENV.E2E_N8N_BASE_URL || '').trim();
  if (!base) return '';
  const path = (__ENV.E2E_N8N_WEBHOOK_PATH || 'webhook/evolution-webhook')
    .trim()
    .replace(/^\//, '');
  return `${base.replace(/\/$/, '')}/${path}`;
}

const n8nUrl = resolveN8nWebhookUrl();

const authToken = (
  __ENV.N8N_AUTH_TOKEN ||
  __ENV.E2E_N8N_TOKEN ||
  ''
).trim();

const instanceName = (__ENV.E2E_INSTANCE_NAME || '').trim();
const tenantId = Number(__ENV.E2E_TENANT_ID || '0');
const scenarioMode = (__ENV.SCENARIO || 'load').toLowerCase();
const isRace = scenarioMode === 'race';

// ─── Özel metrikler ───────────────────────────────────────────────────────
const raceStatus200 = new Counter('race_status_200');
const raceStatus4xx = new Counter('race_status_4xx');
const n8nDbNotReady = new Counter('n8n_db_not_ready');
const raceStatusOther = new Counter('race_status_other');
const webhookErrors = new Counter('webhook_error_logged');

const MESSAGE_POOL = [
  'yarın saat 3e randevu istiyorum',
  'müsait misiniz',
  'merhaba',
  'randevu almak istiyorum',
  'iptal etmek istiyorum',
];

const RACE_SLOT_MESSAGE =
  (__ENV.E2E_RACE_SLOT_MESSAGE || '').trim() ||
  `yarın saat 15:00 için randevu almak istiyorum (${futureSlotIso(1, 15, 0)})`;

function loadTargetVus() {
  const n = Number(__ENV.E2E_LOAD_VUS ?? __ENV.K6_VUS ?? 0);
  if (n > 0) return n;
  return 100;
}

/** AI’lı n8n webhook senkron ~15–45s sürebilir; varsayılan eşikler buna göre. */
function k6Thresholds() {
  const strict = (__ENV.E2E_LOAD_STRICT_THRESHOLDS || '').toLowerCase() === 'true';
  if (strict) {
    return {
      http_req_duration: ['p(95)<3000', 'p(99)<8000'],
      http_req_failed: ['rate<0.05'],
    };
  }
  return {
    http_req_duration: ['p(95)<45000', 'p(99)<90000'],
    http_req_failed: ['rate<0.05'],
  };
}

/** n8n /healthz veya webhook HEAD ile DB hazır olana kadar bekle (setup). */
function waitForN8nReady(maxWaitSec = 120) {
  if ((__ENV.K6_SKIP_N8N_READY || '').toLowerCase() === 'true') return;
  const base = (__ENV.E2E_N8N_BASE_URL || '').trim().replace(/\/$/, '');
  const probeUrl = base ? `${base}/healthz` : n8nUrl;
  if (!probeUrl) return;

  const deadline = Date.now() + maxWaitSec * 1000;
  while (Date.now() < deadline) {
    const res = http.get(probeUrl, { timeout: '10s' });
    if (res.status === 200) {
      const body = (res.body || '').toString();
      if (!body.includes('Database is not ready')) return;
    }
    if (res.status >= 200 && res.status < 500 && res.status !== 503) return;
    sleep(2);
  }
  console.warn(
    `n8n hâlâ hazır görünmüyor (${maxWaitSec}s). Yük testi yine de başlıyor; 503 bekleyin.`,
  );
}

// ─── k6 seçenekleri ────────────────────────────────────────────────────────
export const options = isRace
  ? {
      scenarios: {
        slot_race: {
          executor: 'per-vu-iterations',
          vus: 10,
          iterations: 1,
          maxDuration: '3m',
          exec: 'raceScenario',
        },
      },
      thresholds: k6Thresholds(),
    }
  : {
      scenarios: {
        webhook_flood: {
          executor: 'ramping-vus',
          startVUs: 0,
          stages: [
            { duration: '30s', target: loadTargetVus() },
            { duration: '2m', target: loadTargetVus() },
            { duration: '30s', target: 0 },
          ],
          exec: 'loadScenario',
        },
      },
      thresholds: k6Thresholds(),
    };

/** Ortam doğrulama — eksik değişken varsa test başlamadan hata ver. */
export function setup() {
  const missing = [];
  if (!n8nUrl) {
    missing.push('N8N_WEBHOOK_URL veya E2E_N8N_WEBHOOK_URL veya E2E_N8N_BASE_URL');
  }
  if (!instanceName) missing.push('E2E_INSTANCE_NAME');
  if (!tenantId) missing.push('E2E_TENANT_ID');
  if (missing.length) {
    throw new Error(
      `Eksik env: ${missing.join(', ')}. ` +
        'e2e/.env örneği: E2E_N8N_BASE_URL=http://localhost:5678 veya N8N_WEBHOOK_URL=https://.../webhook/evolution-webhook. ' +
        'npm run test:load:webhook .env dosyasını otomatik yükler.',
    );
  }
  waitForN8nReady();
  return {
    n8nUrl,
    instanceName,
    tenantId,
    mode: scenarioMode,
    raceMessage: RACE_SLOT_MESSAGE,
  };
}

function webhookHeaders() {
  const headers = {
    'Content-Type': 'application/json',
    'ngrok-skip-browser-warning': '1',
  };
  if (authToken) {
    headers['X-Auth-Token'] = authToken;
    headers['X-Tenant-Id'] = String(tenantId);
  }
  return headers;
}

/** Her istek için benzersiz telefon: 5320000 + Date.now() son 3 hane (+ VU çakışmasını azaltır). */
function uniquePhone() {
  const tail = String((Date.now() + __VU + __ITER) % 1000).padStart(3, '0');
  return `5320000${tail}`;
}

function pickRandomMessage() {
  return MESSAGE_POOL[Math.floor(Math.random() * MESSAGE_POOL.length)];
}

function postToN8nWebhook(payload, tagName) {
  return http.post(n8nUrl, JSON.stringify(payload), {
    headers: webhookHeaders(),
    tags: { name: tagName },
    timeout: '120s',
  });
}

function logHttpFailure(res, context) {
  if (res.status >= 200 && res.status < 300) return;
  if (res.status === 503 && res.body.includes('Database is not ready')) {
    n8nDbNotReady.add(1);
    console.warn(
      `[${context}] n8n DB not ready (503) — start n8n and wait for migrations. Set K6_SKIP_N8N_READY=true to skip.`,
    );
    return;
  }
  webhookErrors.add(1);
  const bodyPreview = (res.body || '').toString().slice(0, 400);
  console.warn(
    `[${context}] status=${res.status} duration=${res.timings.duration}ms body=${bodyPreview}`,
  );
}

// ─── Senaryo 1: 100 kullanıcı webhook flood ───────────────────────────────
export function loadScenario() {
  const phone = uniquePhone();
  const text = pickRandomMessage();
  const payload = buildEvolutionPayload(instanceName, phone, text);

  const res = postToN8nWebhook(payload, 'n8n_webhook_load');

  const ok = check(res, {
    'webhook yanıtı 2xx/3xx': (r) => r.status >= 200 && r.status < 400,
  });

  if (!ok) logHttpFailure(res, `load vu=${__VU} phone=${phone}`);

  sleep(0.2 + Math.random() * 0.3);
}

// ─── Senaryo 2: Aynı slot için 10 paralel randevu (race) ───────────────────
export function raceScenario() {
  // Aynı tenant, aynı mesaj/slot; farklı müşteri telefonları
  const phone = `53200009${String(__VU).padStart(2, '0')}`;
  const payload = buildEvolutionPayload(instanceName, phone, RACE_SLOT_MESSAGE);

  const res = postToN8nWebhook(payload, 'n8n_webhook_race');

  if (res.status >= 200 && res.status < 300) {
    raceStatus200.add(1);
  } else if (res.status === 400 || res.status === 409) {
    raceStatus4xx.add(1);
  } else {
    raceStatusOther.add(1);
  }

  logHttpFailure(res, `race vu=${__VU} phone=${phone}`);

  check(res, {
    'race yanıt kaydedildi': () => true,
  });
}

/** Test sonu özet tablo (Türkçe). */
export function handleSummary(data) {
  const m = data.metrics;
  const total = m.http_reqs?.values?.count ?? 0;
  const failRate = (m.http_req_failed?.values?.rate ?? 0) * 100;
  const successPct = (100 - failRate).toFixed(2);
  const avg = m.http_req_duration?.values?.avg ?? 0;
  const p95 = m.http_req_duration?.values['p(95)'] ?? 0;
  const p99 = m.http_req_duration?.values['p(99)'] ?? 0;
  const max = m.http_req_duration?.values?.max ?? 0;

  const lines = [
    '══════════════════════════════════════════════════════',
    `  n8n webhook yük testi özeti (${isRace ? 'RACE' : 'LOAD'})`,
    '══════════════════════════════════════════════════════',
    `  Toplam istek      : ${total}`,
    `  Başarı oranı      : ${successPct}% (http failed rate: ${failRate.toFixed(2)}%)`,
    `  Ort. süre         : ${avg.toFixed(0)} ms`,
    `  p95               : ${p95.toFixed(0)} ms`,
    `  p99               : ${p99.toFixed(0)} ms`,
    `  Max süre          : ${max.toFixed(0)} ms`,
    `  Loglanan hata     : ${m.webhook_error_logged?.values?.count ?? 0}`,
    `  n8n DB not ready (503): ${m.n8n_db_not_ready?.values?.count ?? 0}`,
  ];

  const dbNotReady = m.n8n_db_not_ready?.values?.count ?? 0;
  if (dbNotReady > 0 && !isRace) {
    lines.push('──────────────────────────────────────────────────────');
    lines.push('  ⚠ Çoğu hata n8n tarafında: "Database is not ready!"');
    lines.push('    → n8n’i yeniden başlatın, migration bitsin, workflow Active olsun.');
    lines.push('    → Önce: npm run test:load:webhook:light (10 VU)');
  }

  if (isRace) {
    const ok = m.race_status_200?.values?.count ?? 0;
    const conflict = m.race_status_4xx?.values?.count ?? 0;
    const other = m.race_status_other?.values?.count ?? 0;
    lines.push('──────────────────────────────────────────────────────');
    lines.push('  Race (aynı slot, 10 paralel webhook):');
    lines.push(`    2xx (başarılı)     : ${ok}  (beklenti: ≈1)`);
    lines.push(`    400/409 (çakışma)  : ${conflict}  (beklenti: ≈9)`);
    lines.push(`    Diğer              : ${other}`);
    lines.push(`  Mesaj: ${RACE_SLOT_MESSAGE.slice(0, 80)}…`);
  } else {
    lines.push('──────────────────────────────────────────────────────');
    lines.push(
      `  Load: ${loadTargetVus()} VU, rastgele mesaj havuzu, benzersiz 5320000xxx telefon`,
    );
    const strict = (__ENV.E2E_LOAD_STRICT_THRESHOLDS || '').toLowerCase() === 'true';
    lines.push(
      `  Eşik profili      : ${strict ? 'strict (p95<3s)' : 'AI webhook (p95<45s, fail<5%)'}`,
    );
  }

  lines.push('══════════════════════════════════════════════════════');

  return { stdout: lines.join('\n') + '\n' };
}
