import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter } from 'k6/metrics';
import { loadTenantsFromEnv, authHeaders, buildEvolutionPayload, futureSlotIso } from './lib/tenants.js';

const tenants = loadTenantsFromEnv();
const apiBase = (__ENV.E2E_API_URL || 'http://localhost:5294').replace(/\/$/, '');
const n8nUrl = (__ENV.E2E_N8N_WEBHOOK_URL || '').trim();

const lockOk = new Counter('lock_ok');
const lockConflict = new Counter('lock_conflict');
const createOk = new Counter('race_create_ok');
const createConflict = new Counter('race_create_conflict');

export const options = {
  scenarios: {
    webhook_100: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '10s', target: Math.min(tenants.length, 100) },
        { duration: '3m', target: Math.min(tenants.length, 100) },
        { duration: '30s', target: 0 },
      ],
      exec: 'webhookScenario',
      startTime: '0s',
    },
    slot_race: {
      executor: 'per-vu-iterations',
      vus: 10,
      iterations: 1,
      maxDuration: '2m',
      exec: 'raceScenario',
      startTime: '4m30s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<3000'],
    http_req_failed: ['rate<0.02'],
  },
};

export function webhookScenario() {
  if (!n8nUrl) {
    throw new Error('E2E_N8N_WEBHOOK_URL gerekli (canlı n8n Evolution webhook)');
  }
  const idx = (__VU - 1) % tenants.length;
  const t = tenants[idx];
  const phone = `5320000${String(__VU).padStart(3, '0')}`.slice(0, 11);
  const payload = buildEvolutionPayload(t.instanceName, phone, 'Merhaba k6 yük testi');
  const res = http.post(n8nUrl, JSON.stringify(payload), {
    headers: { 'Content-Type': 'application/json', 'ngrok-skip-browser-warning': '1' },
    tags: { name: 'n8n_evolution_webhook' },
  });
  check(res, { 'webhook accepted': (r) => r.status >= 200 && r.status < 500 });
  sleep(0.3);
}

export function raceScenario() {
  const raceIndex = Number(__ENV.E2E_RACE_TENANT_INDEX ?? '0');
  const t = tenants[raceIndex % tenants.length];
  const startDate =
    (__ENV.E2E_RACE_SLOT_ISO || '').trim() || futureSlotIso(21, 10, 0);
  const customerPhone = `532000099${String(__VU).padStart(1, '0')}`;

  group('lock_then_create', () => {
    const lockRes = http.post(
      `${apiBase}/api/Appointments/lock`,
      JSON.stringify({ tenantId: t.id, startDate }),
      { headers: authHeaders(t.id, t.token), tags: { name: 'slot_lock' } },
    );
    if (lockRes.status === 200) lockOk.add(1);
    if (lockRes.status === 409) lockConflict.add(1);

    const createRes = http.post(
      `${apiBase}/api/Appointments?instanceName=${encodeURIComponent(t.instanceName)}`,
      JSON.stringify({
        customerName: 'K6 Race',
        customerPhone,
        businessPhone: t.instanceName,
        serviceID: t.serviceId,
        appUserID: t.staffId,
        startDate,
      }),
      { headers: authHeaders(t.id, t.token), tags: { name: 'race_create' } },
    );
    if (createRes.status === 200) createOk.add(1);
    if (createRes.status === 400 || createRes.status === 409) createConflict.add(1);
  });
}

export function handleSummary(data) {
  const m = data.metrics;
  const p95 = m.http_req_duration?.values['p(95)'] ?? 0;
  const failRate = ((m.http_req_failed?.values?.rate ?? 0) * 100).toFixed(2);
  const ok = data.metrics.race_create_ok?.values?.count ?? 0;
  const conflict = data.metrics.race_create_conflict?.values?.count ?? 0;

  return {
    stdout: [
      '── WhatsApp webhook load summary ──',
      `http p95: ${p95.toFixed(0)}ms fail rate: ${failRate}%`,
      `Race creates OK: ${ok} conflict/400/409: ${conflict} (beklenti: OK≈1, conflict≈9)`,
    ].join('\n') + '\n',
  };
}
