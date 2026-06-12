import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate } from 'k6/metrics';
import { loadTenantsFromEnv, buildEvolutionPayload } from './lib/tenants.js';

if (__ENV.E2E_RUN_LOAD_AI !== 'true') {
  throw new Error('AI yük testi kapalı — E2E_RUN_LOAD_AI=true ile çalıştırın (Gemini maliyeti).');
}

const tenants = loadTenantsFromEnv();
const n8nUrl = (__ENV.E2E_N8N_WEBHOOK_URL || '').trim();
const geminiRateLimit = new Counter('gemini_rate_limit');
const aiErrors = new Rate('ai_errors');

export const options = {
  scenarios: {
    ai_load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: Math.min(tenants.length, 100) },
        { duration: '5m', target: Math.min(tenants.length, 100) },
        { duration: '1m', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(90)<10000'],
    http_req_failed: ['rate<0.05'],
  },
};

export default function () {
  if (!n8nUrl) {
    throw new Error('E2E_N8N_WEBHOOK_URL gerekli');
  }
  const idx = (__VU - 1) % tenants.length;
  const t = tenants[idx];
  const phone = `5320001${String(__VU).padStart(3, '0')}${String(__ITER).padStart(2, '0')}`.slice(0, 11);
  const payload = buildEvolutionPayload(
    t.instanceName,
    phone,
    'Yarın saat 14 için randevu almak istiyorum',
  );

  const res = http.post(n8nUrl, JSON.stringify(payload), {
    headers: { 'Content-Type': 'application/json', 'ngrok-skip-browser-warning': '1' },
    timeout: '120s',
    tags: { name: 'ai_evolution_webhook' },
  });

  const body = res.body || '';
  if (/rate|limit|429/i.test(body)) geminiRateLimit.add(1);
  const ok = res.status >= 200 && res.status < 500;
  aiErrors.add(!ok);
  check(res, { 'webhook not 5xx': (r) => r.status < 500 });
  sleep(2);
}

export function handleSummary(data) {
  const p90 = data.metrics.http_req_duration?.values['p(90)'] ?? 0;
  const gemini = data.metrics.gemini_rate_limit?.values?.count ?? 0;
  const fail = ((data.metrics.http_req_failed?.values?.rate ?? 0) * 100).toFixed(2);
  return {
    stdout: [
      '── AI response load summary ──',
      `p90: ${p90.toFixed(0)}ms fail: ${fail}% gemini_rate_limit hits: ${gemini}`,
      'Tenant karışımı: her VU benzersiz customerPhone — my-active-appointments manuel doğrulama önerilir.',
    ].join('\n') + '\n',
  };
}
