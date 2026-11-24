<#
.SYNOPSIS
Fails when a ZAP report holds a finding above low that is not accepted in tests/security/zap-accepted.json.

.DESCRIPTION
Reads the JSON report of zap-api-scan.py (-J) and the list of accepted findings. A finding is an alert of ZAP;
its risk is 0 informational, 1 low, 2 medium, 3 high. Findings at or above -FailAt (default medium) that are
not accepted fail the check (exit 1). An accepted finding is matched by its ZAP plugin id and needs a reason in
the file; an entry without a reason fails too. Prints the accepted findings it met (with the reason), the
accepted entries that matched nothing (stale: remove them) and the findings below -FailAt as a count.

.PARAMETER Report
The report.json written by ZAP.

.PARAMETER Accepted
The accepted findings. Default tests/security/zap-accepted.json.

.PARAMETER FailAt
The lowest risk that fails: low, medium (default) or high.

.EXAMPLE
pwsh scripts/check-zap-report.ps1 -Report zap-wrk/report.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Report,

    [string]$Accepted = (Join-Path (Split-Path -Parent $PSScriptRoot) 'tests/security/zap-accepted.json'),

    [ValidateSet('low', 'medium', 'high')]
    [string]$FailAt = 'medium'
)

$ErrorActionPreference = 'Stop'
$threshold = @{ low = 1; medium = 2; high = 3 }[$FailAt]
$names = @('informational', 'low', 'medium', 'high')

$entries = @((Get-Content $Accepted -Raw | ConvertFrom-Json).accepted)
$problems = New-Object System.Collections.Generic.List[string]
foreach ($a in $entries) {
    if ([string]::IsNullOrWhiteSpace($a.reason)) { $problems.Add("The accepted finding $($a.pluginId) ($($a.name)) has no reason.") }
}

$alerts = @((Get-Content $Report -Raw | ConvertFrom-Json).site | ForEach-Object { $_.alerts })
$met = @{}
$failing = New-Object System.Collections.Generic.List[string]
$below = 0
foreach ($alert in $alerts) {
    $risk = [int]$alert.riskcode
    $entry = $entries | Where-Object { [string]$_.pluginId -eq [string]$alert.pluginid } | Select-Object -First 1
    if ($entry) { $met[[string]$alert.pluginid] = $true }
    if ($risk -lt $threshold) { $below++; continue }
    $line = "[$($names[$risk])] $($alert.name) (plugin $($alert.pluginid), $(@($alert.instances).Count) instances)"
    if ($entry) { Write-Host "accepted: $line. Reason: $($entry.reason)" }
    else { $failing.Add($line) }
}

foreach ($a in $entries) {
    if (-not $met.ContainsKey([string]$a.pluginId)) { Write-Host "stale: accepted finding $($a.pluginId) ($($a.name)) matched nothing in this report; remove it if it stays gone." }
}
Write-Host "$below finding(s) below $FailAt, not checked."

foreach ($f in $failing) { Write-Host "::error::ZAP finding not accepted: $f" }
foreach ($p in $problems) { Write-Host "::error::$p" }
if ($failing.Count -gt 0 -or $problems.Count -gt 0) {
    Write-Host "Fix the finding, or accept it with a reason in tests/security/zap-accepted.json."
    exit 1
}
Write-Host "ZAP: no finding at or above $FailAt that is not accepted."
