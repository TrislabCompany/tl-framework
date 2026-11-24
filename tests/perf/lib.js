// Shared by the smoke and the load scripts: the sign-in, the seed data and the request mix.
import http from 'k6/http';
import { check, fail } from 'k6';

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5040';
export const TENANT = __ENV.TENANT || 'root';
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || 'admin@root.com';
// The development password of the seeded administrator (src/Tests/TestHost/appsettings.Development.json).
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || '123Pa$$word!';
const SEED_USERS = parseInt(__ENV.SEED_USERS || '40', 10);
const SEED_NOTIFICATIONS = parseInt(__ENV.SEED_NOTIFICATIONS || '60', 10);

const JSON_HEADERS = { 'Content-Type': 'application/json', tenant: TENANT };

// Runs once before the test. The host seeded the root tenant, its administrator and the roles when it
// started; this adds rows so that the list endpoints page through real data: users and notifications.
export function seed() {
  const login = http.post(
    `${BASE_URL}/api/v1/identity/token/issue`,
    JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD }),
    { headers: JSON_HEADERS },
  );
  if (login.status !== 200) {
    fail(`Sign-in failed: ${login.status} ${login.body}`);
  }
  const token = login.json('accessToken');
  const headers = { ...JSON_HEADERS, Authorization: `Bearer ${token}` };

  const profile = http.get(`${BASE_URL}/api/v1/identity/profile`, { headers });
  const adminId = profile.json('id');

  const run = Date.now();
  for (let i = 0; i < SEED_USERS; i++) {
    http.post(
      `${BASE_URL}/api/v1/identity/register`,
      JSON.stringify({
        firstName: 'Perf',
        lastName: `User${i}`,
        email: `perf-${run}-${i}@example.com`,
        userName: `perf-${run}-${i}`,
        password: 'Perf-Seed-Pa55word!',
        confirmPassword: 'Perf-Seed-Pa55word!',
      }),
      { headers },
    );
  }
  for (let i = 0; i < SEED_NOTIFICATIONS; i++) {
    http.post(
      `${BASE_URL}/api/v1/test/notifications`,
      JSON.stringify({ userId: adminId, type: 'perf', title: `Seed ${i}`, body: 'Seed data of the performance run' }),
      { headers },
    );
  }

  // Warm-up: the first call of each endpoint pays for JIT compilation and cold caches, which a budget
  // must not count. These requests are not tagged, so no threshold sees them.
  http.get(`${BASE_URL}/api/health/ready`);
  for (const e of ENDPOINTS) {
    http.get(`${BASE_URL}${e.path}`, { headers });
  }
  return { token, adminId };
}

// The main endpoints of the host, with the tag each request carries. The thresholds in smoke.js and
// load.js are set per tag, so a slow endpoint is named in the result.
export const ENDPOINTS = [
  { name: 'profile', path: '/api/v1/identity/profile' },
  { name: 'users_list', path: '/api/v1/identity/users' },
  { name: 'roles_list', path: '/api/v1/identity/roles' },
  { name: 'groups_list', path: '/api/v1/identity/groups' },
  { name: 'permissions_me', path: '/api/v1/identity/permissions' },
  { name: 'notifications_list', path: '/api/v1/notifications' },
  { name: 'notifications_unread', path: '/api/v1/notifications/unread-count' },
  { name: 'audits_list', path: '/api/v1/audits' },
  { name: 'webhooks_list', path: '/api/v1/webhooks/subscriptions?pageNumber=1&pageSize=20' },
  { name: 'billing_plans', path: '/api/v1/billing/plans?includeInactive=false' },
  { name: 'files_mine', path: '/api/v1/files/mine' },
  { name: 'invoice_customers', path: '/api/v1/invoice-documents/customers?pageNumber=1&pageSize=20' },
  { name: 'tenant_status', path: '/api/v1/tenants/me/status' },
];

export function authedGets(data) {
  const headers = { Authorization: `Bearer ${data.token}`, tenant: TENANT };
  for (const e of ENDPOINTS) {
    const res = http.get(`${BASE_URL}${e.path}`, { headers, tags: { endpoint: e.name } });
    check(res, { [`${e.name} is 200`]: (r) => r.status === 200 }, { endpoint: e.name });
  }
}

export function signIn() {
  const res = http.post(
    `${BASE_URL}/api/v1/identity/token/issue`,
    JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD }),
    { headers: JSON_HEADERS, tags: { endpoint: 'token_issue' } },
  );
  check(res, { 'token_issue is 200': (r) => r.status === 200 }, { endpoint: 'token_issue' });
}

export function probes() {
  const res = http.get(`${BASE_URL}/api/health/ready`, { tags: { endpoint: 'health_ready' } });
  check(res, { 'health_ready is 200': (r) => r.status === 200 }, { endpoint: 'health_ready' });
}
