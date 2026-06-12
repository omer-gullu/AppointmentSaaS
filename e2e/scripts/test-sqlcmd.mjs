import { config } from 'dotenv';
import { execSync } from 'child_process';

config({ path: '.env' });

const host = process.env.E2E_DB_SERVER?.trim() ?? 'OMER';
const instance = process.env.E2E_DB_INSTANCE?.trim();
const server = instance ? `${host}\\${instance}` : host;
const db = process.env.E2E_DB_NAME?.trim() ?? 'AppointmentSaaSDB';
const q =
  'SET NOCOUNT ON; UPDATE Tenants SET IsActive = 1, IsSubscriptionActive = 1 WHERE TenantID = 2165; UPDATE AppUsers SET LockoutEnd = NULL, AccessFailedCount = 0 WHERE TenantID = 2165';
const cmd = `sqlcmd -S "${server}" -d "${db}" -E -Q "${q}" -h-1 -W`;

console.log('Server:', server);
console.log('Cmd:', cmd);
try {
  const out = execSync(cmd, { encoding: 'utf8', timeout: 60_000, windowsHide: true });
  console.log('OK:', out.trim());
} catch (e) {
  console.error('FAIL:', e.message);
  if (e.stderr) console.error(String(e.stderr));
  process.exit(1);
}
