import http from 'k6/http';
import { check, sleep } from 'k6';
import { loadTenantsFromEnv, authHeaders, futureSlotIso } from './lib/tenants.js';

const tenants = loadTenantsFromEnv();
const apiBase = (__ENV.E2E_API_URL || 'http://localhost:5294').replace(/\/$/, '');

export const options = {
  scenarios: {
    appointments: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: Math.min(tenants.length, 100) },
        { duration: '2m', target: Math.min(tenants.length, 100) },
        { duration: '30s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<2000'],
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  const idx = (__VU - 1) % tenants.length;
  const t = tenants[idx];
  const phoneSuffix = String((__VU * 1000 + __ITER) % 1000).padStart(3, '0');
  const customerPhone = `5320000${String(__VU).padStart(2, '0')}${phoneSuffix}`.slice(0, 11);
  const startDate = futureSlotIso(14 + (__VU % 30), 9 + (__VU % 8), (__ITER * 5) % 60);

  const url = `${apiBase}/api/Appointments?instanceName=${encodeURIComponent(t.instanceName)}`;
  const body = JSON.stringify({
    customerName: 'K6 Load Test',
    customerPhone,
    businessPhone: t.instanceName,
    serviceID: t.serviceId,
    appUserID: t.staffId,
    startDate,
  });

  const res = http.post(url, body, { headers: authHeaders(t.id, t.token), tags: { name: 'appointment_create' } });
  check(res, {
    'status 200': (r) => r.status === 200,
  });
  sleep(0.5);
}

export function handleSummary(data) {
  const m = data.metrics;
  const total = m.http_reqs?.values?.count ?? 0;
  const failed = m.http_req_failed?.values?.rate ?? 0;
  const avg = m.http_req_duration?.values?.avg ?? 0;
  const p95 = m.http_req_duration?.values['p(95)'] ?? 0;
  const max = m.http_req_duration?.values?.max ?? 0;
  const successPct = ((1 - failed) * 100).toFixed(2);

  const lines = [
    '── Appointment load summary ──',
    `Total requests: ${total}`,
    `Success rate: ${successPct}%`,
    `Duration avg: ${avg.toFixed(0)}ms p95: ${p95.toFixed(0)}ms max: ${max.toFixed(0)}ms`,
  ];
  return { stdout: lines.join('\n') + '\n' };
}
