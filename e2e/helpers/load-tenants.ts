import fs from 'fs';
import path from 'path';

/** K6 yük testi için tek tenant kaydı. */
export type LoadTenantEntry = {
  id: number;
  token: string;
  instanceName: string;
  serviceId: number;
  staffId: number;
};

export type LoadTenantsFile = {
  tenants: LoadTenantEntry[];
};

/**
 * E2E_LOAD_TENANTS_JSON: dosya yolu veya inline JSON.
 * Örnek dosya: e2e/fixtures/load-tenants.json (discover-env.ps1 ile üretilir).
 */
export function loadTenantsFromEnv(): LoadTenantEntry[] {
  const raw = process.env.E2E_LOAD_TENANTS_JSON?.trim();
  if (!raw) {
    throw new Error(
      'E2E_LOAD_TENANTS_JSON gerekli — dosya yolu veya JSON. scripts/discover-env.ps1 -ExportLoadTenants',
    );
  }

  let content = raw;
  if (!raw.startsWith('{') && !raw.startsWith('[')) {
    const resolved = path.isAbsolute(raw) ? raw : path.resolve(process.cwd(), raw);
    if (!fs.existsSync(resolved)) {
      throw new Error(`E2E_LOAD_TENANTS_JSON dosyası bulunamadı: ${resolved}`);
    }
    content = fs.readFileSync(resolved, 'utf8');
  }

  const parsed = JSON.parse(content) as LoadTenantsFile | LoadTenantEntry[];
  const list = Array.isArray(parsed) ? parsed : parsed.tenants;
  if (!list?.length) {
    throw new Error('E2E_LOAD_TENANTS_JSON: tenants dizisi boş');
  }

  for (const t of list) {
    if (!t.id || !t.token?.trim() || !t.instanceName?.trim() || !t.serviceId || !t.staffId) {
      throw new Error(`Geçersiz tenant kaydı: ${JSON.stringify(t)}`);
    }
  }

  return list;
}
