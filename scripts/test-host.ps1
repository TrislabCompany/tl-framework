<#
.SYNOPSIS
Runs the test host (src/Tests/TestHost) as a container with its seed data, for the performance and security runs.

.DESCRIPTION
Builds the host's image, starts it with Postgres and the S3-compatible storage from
src/Tests/TestHost/compose.yaml (`--profile container`), waits until /readyz answers, and can print an
access token of the seeded administrator. The host migrates and seeds itself at startup
(TestHost__Provision): the root tenant, its administrator `admin@root.com` and the roles.

  up     Build the image, start the three containers, wait for /readyz. Idempotent.
  token  Print an access token of the seeded administrator on stdout (nothing else).
  down   Stop and remove the containers and their volumes.
  logs   Print the host's log.

Used by .github/workflows/perf-smoke.yml, perf-nightly.yml and by hand (tests/perf/README.md).
Needs Docker with Compose v2. Everything runs on localhost:5040 (host), 5433 (Postgres), 9100 (storage). On Windows port 5040 belongs to
a system service: use -Port 5041.

.PARAMETER Action
up, token, down or logs.

.PARAMETER Port
The port the host is published on. Default 5040.

.PARAMETER TimeoutSeconds
How long `up` waits for /readyz. Default 180.

.EXAMPLE
pwsh scripts/test-host.ps1 up
$token = pwsh scripts/test-host.ps1 token
pwsh scripts/test-host.ps1 down
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('up', 'token', 'down', 'logs')]
    [string]$Action,

    [int]$Port = 5040,

    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$BaseUrl = "http://localhost:$Port"
$env:TEST_HOST_PORT = $Port
$repo = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $repo 'src/Tests/TestHost/compose.yaml'

function Invoke-Compose {
    docker compose -f $compose --profile container @args
    if ($LASTEXITCODE -ne 0) { throw "docker compose $($args -join ' ') failed ($LASTEXITCODE)" }
}

switch ($Action) {
    'up' {
        Invoke-Compose up --build --detach
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while ($true) {
            try {
                $r = Invoke-WebRequest -Uri "$BaseUrl/readyz" -UseBasicParsing -TimeoutSec 5
                if ($r.StatusCode -eq 200) { break }
            }
            catch { Write-Verbose $_.Exception.Message }
            if ((Get-Date) -gt $deadline) {
                Invoke-Compose logs host --tail 80
                throw "The test host did not answer $BaseUrl/readyz within $TimeoutSeconds seconds."
            }
            Start-Sleep -Seconds 2
        }
        Write-Host "The test host is ready at $BaseUrl."
    }
    'token' {
        # The development password of the seeded administrator is Seed:DefaultAdminPassword in
        # src/Tests/TestHost/appsettings.Development.json.
        $dev = Get-Content (Join-Path $repo 'src/Tests/TestHost/appsettings.Development.json') -Raw | ConvertFrom-Json
        $body = @{ email = 'admin@root.com'; password = $dev.Seed.DefaultAdminPassword } | ConvertTo-Json
        $r = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/identity/token/issue" -Headers @{ tenant = 'root' } `
            -ContentType 'application/json' -Body $body
        $r.accessToken
    }
    'down' { Invoke-Compose down --volumes --remove-orphans }
    'logs' { Invoke-Compose logs host --tail 200 }
}
