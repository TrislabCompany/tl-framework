// The smoke test: every pull request. One virtual user per scenario, about 30 seconds in all, against the
// test host running as a container (scripts/test-host.ps1 up). It checks that the main endpoints answer
// and stay inside their time budget. A missed budget fails the run (k6 exits 99), and so the check.
//
// The budgets are p95 per endpoint in milliseconds. On a GitHub-hosted runner (2 vCPUs, Postgres on the same
// machine) the first run measured 1 to 14 ms for the reads (users list 12 ms) and 88 ms for the sign-in, which
// hashes a password. The budgets are about 10 times that, with a floor of 100 ms: a real slowdown (a missing
// index, an N+1 query, a blocking call) crosses them, runner noise does not. Tighten them from the p95 of the
// first weeks of runs (the artifact k6-smoke-summary); loosen one only for a cause you can name.
import { sleep } from 'k6';
import { seed, authedGets, signIn, probes } from './lib.js';

export const options = {
  scenarios: {
    // One user walks the read endpoints in a loop.
    reads: { executor: 'constant-vus', vus: 2, duration: '20s', exec: 'reads' },
    // One user signs in now and then: the slow path (password hash).
    sign_in: { executor: 'constant-arrival-rate', rate: 1, timeUnit: '1s', duration: '20s', preAllocatedVUs: 2, exec: 'signInScenario' },
  },
  thresholds: {
    // Failures: under 1 percent of all requests, and every check passes.
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    // Budgets, per endpoint (the `endpoint` tag), p95 in milliseconds.
    'http_req_duration{endpoint:health_ready}': ['p(95)<150'],
    'http_req_duration{endpoint:profile}': ['p(95)<100'],
    'http_req_duration{endpoint:users_list}': ['p(95)<150'],
    'http_req_duration{endpoint:roles_list}': ['p(95)<100'],
    'http_req_duration{endpoint:groups_list}': ['p(95)<150'],
    'http_req_duration{endpoint:permissions_me}': ['p(95)<100'],
    'http_req_duration{endpoint:notifications_list}': ['p(95)<100'],
    'http_req_duration{endpoint:notifications_unread}': ['p(95)<100'],
    'http_req_duration{endpoint:audits_list}': ['p(95)<150'],
    'http_req_duration{endpoint:webhooks_list}': ['p(95)<100'],
    'http_req_duration{endpoint:billing_plans}': ['p(95)<100'],
    'http_req_duration{endpoint:files_mine}': ['p(95)<100'],
    'http_req_duration{endpoint:invoice_customers}': ['p(95)<100'],
    'http_req_duration{endpoint:tenant_status}': ['p(95)<100'],
    'http_req_duration{endpoint:token_issue}': ['p(95)<400'],
  },
};

export function setup() {
  return seed();
}

export function reads(data) {
  probes();
  authedGets(data);
  sleep(0.5);
}

export function signInScenario() {
  signIn();
}
