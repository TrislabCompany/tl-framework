// The nightly load run: the same requests as the smoke test, with more users and a longer time, to see
// how the host behaves under sustained load and to keep the numbers (the workflow uploads summary.json).
// It ramps up, holds, and ramps down: 8 minutes in all. The budgets are the smoke budgets with room for
// queueing on a runner; a missed budget fails the run.
import { sleep } from 'k6';
import { seed, authedGets, signIn, probes } from './lib.js';

export const options = {
  scenarios: {
    reads: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '1m', target: 10 },
        { duration: '5m', target: 20 },
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '15s',
      exec: 'reads',
    },
    sign_in: { executor: 'constant-arrival-rate', rate: 2, timeUnit: '1s', duration: '7m', preAllocatedVUs: 5, maxVUs: 20, exec: 'signInScenario' },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    'http_req_duration{endpoint:health_ready}': ['p(95)<500'],
    'http_req_duration{endpoint:profile}': ['p(95)<800'],
    'http_req_duration{endpoint:users_list}': ['p(95)<1200'],
    'http_req_duration{endpoint:roles_list}': ['p(95)<800'],
    'http_req_duration{endpoint:groups_list}': ['p(95)<800'],
    'http_req_duration{endpoint:permissions_me}': ['p(95)<800'],
    'http_req_duration{endpoint:notifications_list}': ['p(95)<800'],
    'http_req_duration{endpoint:notifications_unread}': ['p(95)<600'],
    'http_req_duration{endpoint:audits_list}': ['p(95)<1500'],
    'http_req_duration{endpoint:webhooks_list}': ['p(95)<800'],
    'http_req_duration{endpoint:billing_plans}': ['p(95)<800'],
    'http_req_duration{endpoint:files_mine}': ['p(95)<800'],
    'http_req_duration{endpoint:invoice_customers}': ['p(95)<800'],
    'http_req_duration{endpoint:tenant_status}': ['p(95)<800'],
    'http_req_duration{endpoint:token_issue}': ['p(95)<4000'],
  },
};

export function setup() {
  return seed();
}

export function reads(data) {
  probes();
  authedGets(data);
  sleep(0.2);
}

export function signInScenario() {
  signIn();
}
