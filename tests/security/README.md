# Security tests

Two checks on the test host (`src/Tests/TestHost`), both run on every pull request.

## 1. Every endpoint needs sign-in unless it is marked public

`src/Tests/Integration.Tests/Tests/Security/EndpointAuthorizationTests.cs`, part of `dotnet test src/App.slnx`.

The framework is secure by default: the fallback policy requires a signed-in user on every endpoint that has no
authorization metadata, so a forgotten `RequireAuthorization()` is still protected. Public is an explicit act:
`AllowAnonymous()`. The test checks that:

- the fallback and default policies deny anonymous users;
- the endpoints marked `AllowAnonymous()` are exactly the list `AnonymousEndpoints` in the test, each with a reason. A
  new public endpoint fails the test until it is listed; an entry that matches nothing fails too, so the list never
  goes stale;
- the documentation paths that `PathAwareAuthorizationHandler` lets through (`/openapi`, `/scalar`) are exactly
  `PublicPathPrefixes`;
- every other endpoint, in every method it maps, answers a request without a token with `401`, before its handler runs
  (this is the behavioural check; it also catches an endpoint that a middleware or a handler lets through).

To make an endpoint public, add `AllowAnonymous()` and a line to `AnonymousEndpoints` with the reason in the same pull
request. A reviewer reads that list.

## 2. A ZAP scan of the running test host

`.github/workflows/security-scan.yml` (check `security-scan / zap`).

It starts the host as a container (`scripts/test-host.ps1 up`, [../perf/README.md](../perf/README.md) has the same
setup), downloads its OpenAPI document (`/openapi/v1.json`) and runs `zap-api-scan.py -f openapi` from the pinned
[OWASP ZAP](https://www.zaproxy.org) image **in safe mode** (`-S`): ZAP sends the documented requests, signed in as the
seeded administrator (a `replacer` rule adds the `Authorization` and `tenant` headers), and runs its passive rules on the
answers. It does not attack. The OpenAPI import fills the parameters with placeholders, so many requests are answered
`400`; what the scan covers is the headers, cookies, caching, information in URLs and in error answers, not the
business logic.

`scripts/check-zap-report.ps1` reads the JSON report and **fails on a finding of medium risk or higher** that is not in
`tests/security/zap-accepted.json`. Each accepted finding is a ZAP plugin id, a name and a **reason**; an entry
without a reason fails, and an entry that matched nothing is reported as stale. The report (`report.html`,
`report.json`) is the artifact `zap-report`, kept for 14 days.

Run it locally (Docker; on Windows use `-Port 5041` and `host.docker.internal`, see [../perf/README.md](../perf/README.md)):

```bash
pwsh scripts/test-host.ps1 up
mkdir -p /tmp/zap && chmod 777 /tmp/zap && curl -s http://localhost:5040/openapi/v1.json -o /tmp/zap/openapi.json
token=$(pwsh scripts/test-host.ps1 token)
docker run --rm --network host -v /tmp/zap:/zap/wrk:rw ghcr.io/zaproxy/zaproxy:2.16.1 zap-api-scan.py \
  -t /zap/wrk/openapi.json -f openapi -O http://localhost:5040 -S -I -J report.json -r report.html \
  -z "-config replacer.full_list(0).description=auth -config replacer.full_list(0).enabled=true -config replacer.full_list(0).matchtype=REQ_HEADER -config replacer.full_list(0).matchstring=Authorization -config replacer.full_list(0).regex=false -config replacer.full_list(0).replacement=Bearer\ $token -config replacer.full_list(1).description=tenant -config replacer.full_list(1).enabled=true -config replacer.full_list(1).matchtype=REQ_HEADER -config replacer.full_list(1).matchstring=tenant -config replacer.full_list(1).regex=false -config replacer.full_list(1).replacement=root"
pwsh scripts/check-zap-report.ps1 -Report /tmp/zap/report.json
pwsh scripts/test-host.ps1 down
```

Accepting a finding: put it in `zap-accepted.json` with the plugin id (shown in the report) and why it is not a risk
for the framework. Do not accept a finding to make a run pass without understanding it.

## What the first run found

Nothing of medium risk or higher, so no finding is accepted yet. ZAP reported five low and informational alerts
(a newer ZAP release exists, 4xx answers to placeholder parameters, an authentication request identified,
identifiers in query strings, non-storable content): not checked, and not a failure.
