# Performance tests (k6)

[k6](https://k6.io) scripts that run against the test host (`src/Tests/TestHost`) **as a container**, with its seed
data. Two runs:

| Run | Script | When | Size | Fails when |
| --- | --- | --- | --- | --- |
| Smoke | `smoke.js` | every pull request (`.github/workflows/perf-smoke.yml`, check `perf-smoke / smoke`) | about 30 seconds, 2 users plus 1 sign-in per second | a p95 budget is missed, or more than 1 percent of requests fail |
| Load | `load.js` | every night at 02:17 UTC and by hand (`.github/workflows/perf-nightly.yml`) | 8 minutes, ramp to 20 users plus 2 sign-ins per second | the same, with looser budgets |

A missed budget makes k6 exit with code 99, which fails the job. The nightly result (`summary.json`) is kept as the
artifact `k6-load-summary-<run>` for 90 days, and its per-endpoint p95 is in the job summary.

## What is measured

Every request carries an `endpoint` tag; the thresholds are set per tag, so the result names the slow endpoint.
`lib.js` holds the list: the readiness probe, the sign-in (password hash, the slowest path), and the main reads of
Identity (me, users, roles, groups, permissions), Notifications (list, unread count), Auditing (list), Webhooks
(subscriptions list) and Multitenancy (own tenant status).

**Seed data.** The host migrates and seeds itself at startup: the root tenant, its administrator and the roles.
`setup()` in `lib.js` then adds 40 users and 60 notifications (`SEED_USERS`, `SEED_NOTIFICATIONS`), so the list
endpoints page through real rows and audit rows from those writes.

## The budgets

Budgets are p95 in milliseconds per endpoint (`options.thresholds` of each script). The first run on a GitHub-hosted
2-vCPU runner, with the whole stack on that machine, measured 1 to 14 ms for the reads and 88 ms for the sign-in
(a password hash). The smoke budgets are about 10 times that, with a floor of 100 ms (reads 100 to 150 ms, sign-in
400 ms), so that a real regression (a missing index, an N+1 query, a blocking call) fails and runner noise does not.
The nightly budgets are twice the smoke ones (sign-in 1000 ms), for queueing under 20 users. Tighten them from the
first weeks of numbers (the artifacts). Docker Desktop on Windows or macOS is several times slower than that runner (sign-in 500 to 700 ms), so a local run can miss a budget that CI meets: read the numbers, not only the verdict. Change a budget in the script, with the reason in the pull request. Do not
raise one to make a failing run pass without finding what got slower.

## Run it locally

Needs Docker. No k6 install: k6 runs from its image.

```bash
pwsh scripts/test-host.ps1 up                      # builds the image, starts Postgres, storage and the host, waits for /readyz
docker run --rm --network host -v "$PWD/tests/perf:/perf:ro" \
  grafana/k6:1.4.2 run /perf/smoke.js              # or /perf/load.js
pwsh scripts/test-host.ps1 down
```

With Docker Desktop, `--network host` may not reach the host: use `-e BASE_URL=http://host.docker.internal:5040`
and drop `--network host`. On Windows port 5040 belongs to a system service: run `up -Port 5041` and
`-e BASE_URL=http://host.docker.internal:5041`. The script's other commands are `token` (prints an access token of the
seeded administrator) and `logs`.
