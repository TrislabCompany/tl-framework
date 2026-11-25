<#
.SYNOPSIS
Runs only the tests a change affects: the .NET test projects, the integration-test areas and the npm packages.

.DESCRIPTION
Takes the changed paths (from git against -Base, or from -ChangedFiles, one path per line) and decides:

- Everything, when a path is listed under "all" in src/Tests/affected-map.json (shared build files, the
  core building blocks, the test host and the test infrastructure), when a path is in no project and not
  ignored, or with -All.
- Otherwise each changed project's test projects: the projects under src/Tests that reach it through
  ProjectReference. Integration.Tests reaches every module through the test host, so for it only the
  areas (folders of Integration.Tests/Tests) of the changed projects run, by the map's "areas", plus
  every area whose own tests changed.
- Nothing, when every path is ignored (documentation, the agent files, the performance and security
  tests, which have their own workflows).

Architecture.Tests runs whenever any .NET project changed. Each npm package in packages/ runs its tests
when a path inside it changed. Prints the plan; with -Run it builds once and runs it, and writes the plan
to the GitHub step summary when GITHUB_STEP_SUMMARY is set.

.PARAMETER Base
The git ref to compare with. Default: origin/main. The comparison is from their merge base, so commits
on main after the branch started don't count.

.PARAMETER ChangedFiles
A file with the changed paths, one per line, relative to the repository root. CI passes it; it wins
over -Base.

.PARAMETER All
Run everything.

.PARAMETER Run
Build and run the plan. Without it, only the plan is printed.

.EXAMPLE
pwsh scripts/affected-tests.ps1                # the plan for this branch against origin/main

.EXAMPLE
pwsh scripts/affected-tests.ps1 -Run           # build once, then run the affected tests

.EXAMPLE
pwsh scripts/affected-tests.ps1 -ChangedFiles changed.txt -Run
#>
[CmdletBinding()]
param(
    [string]$Base = 'origin/main',
    [string]$ChangedFiles,
    [switch]$All,
    [switch]$Run
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$map = Get-Content (Join-Path $root 'src/Tests/affected-map.json') -Raw | ConvertFrom-Json -AsHashtable

function ConvertTo-Regex([string]$glob) {
    # ** spans folders, * stays inside one; a pattern ending in /** matches everything below it.
    $escaped = [regex]::Escape($glob) -replace '\\\*\\\*/', '(?:.*/)?' -replace '\\\*\\\*', '.*' -replace '\\\*', '[^/]*'
    return "^$escaped$"
}

function Test-Glob([string]$path, [object[]]$globs) {
    foreach ($glob in $globs) {
        if ($path -match (ConvertTo-Regex $glob)) { return $true }
    }
    return $false
}

# The changed paths
if ($ChangedFiles) {
    $paths = @(Get-Content $ChangedFiles | Where-Object { $_.Trim() })
} else {
    $mergeBase = git -C $root merge-base $Base HEAD
    if ($LASTEXITCODE -ne 0) { throw "No merge base with $Base. Fetch it first: git fetch origin main" }
    $paths = @(git -C $root diff --name-only $mergeBase) + @(git -C $root ls-files --others --exclude-standard)
}
$paths = @($paths | ForEach-Object { $_ -replace '\\', '/' } | Sort-Object -Unique)

# The projects and who references them
$projects = @{}
foreach ($file in Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.csproj) {
    $dir = ([IO.Path]::GetRelativePath($root, $file.DirectoryName)) -replace '\\', '/'
    $refs = ([xml](Get-Content $file.FullName -Raw)).SelectNodes('//ProjectReference') |
        ForEach-Object { [IO.Path]::GetFileNameWithoutExtension(($_.Include -replace '\\', '/')) }
    $projects[$file.BaseName] = [pscustomobject]@{ Name = $file.BaseName; Dir = $dir; Refs = @($refs) }
}
$referencedBy = @{}
foreach ($p in $projects.Values) {
    foreach ($r in $p.Refs) {
        if (-not $referencedBy.ContainsKey($r)) { $referencedBy[$r] = [Collections.Generic.List[string]]::new() }
        $referencedBy[$r].Add($p.Name)
    }
}

function Get-Project([string]$path) {
    $match = $projects.Values | Where-Object { $path.StartsWith("$($_.Dir)/", [StringComparison]::Ordinal) } |
        Sort-Object { $_.Dir.Length } -Descending | Select-Object -First 1
    return $match
}

$everything = [bool]$All
$why = [Collections.Generic.List[string]]::new()
if ($All) { $why.Add('-All') }
$changedProjects = [Collections.Generic.HashSet[string]]::new()
$areas = [Collections.Generic.SortedSet[string]]::new()
$npm = [Collections.Generic.SortedSet[string]]::new()
$areasRoot = 'src/Tests/Integration.Tests/Tests/'

foreach ($path in $paths) {
    if ($everything) { break }
    if (Test-Glob $path $map['all']) { $everything = $true; $why.Add($path); break }
    if (Test-Glob $path $map['ignore']) { continue }
    if ($path -match '^packages/([^/]+)/') { [void]$npm.Add($Matches[1]); continue }
    if ($path.StartsWith($areasRoot, [StringComparison]::Ordinal)) {
        [void]$areas.Add($path.Substring($areasRoot.Length).Split('/')[0])
        [void]$changedProjects.Add('Integration.Tests')
        continue
    }
    $project = Get-Project $path
    if (-not $project) { $everything = $true; $why.Add("$path (in no project)"); break }
    [void]$changedProjects.Add($project.Name)
}

# Every project that reaches a changed one
$affected = [Collections.Generic.HashSet[string]]::new()
$queue = [Collections.Generic.Queue[string]]::new()
foreach ($name in $changedProjects) { [void]$affected.Add($name); $queue.Enqueue($name) }
while ($queue.Count -gt 0) {
    $name = $queue.Dequeue()
    if ($referencedBy.ContainsKey($name)) {
        foreach ($parent in $referencedBy[$name]) {
            if ($affected.Add($parent)) { $queue.Enqueue($parent) }
        }
    }
}

$testProjects = [Collections.Generic.SortedSet[string]]::new()
if (-not $everything) {
    foreach ($name in $affected) {
        $p = $projects[$name]
        if ($p -and $p.Dir.StartsWith('src/Tests/', [StringComparison]::Ordinal) -and $name -like '*.Tests') {
            [void]$testProjects.Add($name)
        }
    }
    if ($changedProjects.Count -gt 0) { [void]$testProjects.Add('Architecture.Tests') }

    # Integration.Tests: the areas of every affected source project
    if ($testProjects.Contains('Integration.Tests')) {
        foreach ($name in $affected) {
            if ($map['areas'].ContainsKey($name)) {
                foreach ($area in $map['areas'][$name]) { [void]$areas.Add($area) }
            } elseif ($projects[$name] -and $projects[$name].Dir -match '^src/(BuildingBlocks|Modules)/') {
                $everything = $true; $why.Add("$name has no entry in src/Tests/affected-map.json")
            }
        }
    }
}

# The plan
$lines = [Collections.Generic.List[string]]::new()
if ($everything) {
    $lines.Add("Everything runs: $($why -join ', ')")
} elseif ($testProjects.Count -eq 0 -and $npm.Count -eq 0) {
    $lines.Add('No tests are affected')
} else {
    foreach ($t in $testProjects) {
        if ($t -eq 'Integration.Tests') { $lines.Add("$t, areas: $($areas -join ', ')") } else { $lines.Add($t) }
    }
    foreach ($n in $npm) { $lines.Add("packages/$n (npm test)") }
}
"Changed paths: $($paths.Count)"
$lines | ForEach-Object { "  $_" }
if ($env:GITHUB_STEP_SUMMARY) {
    @('### Affected tests', '') + ($lines | ForEach-Object { "- $_" }) | Add-Content $env:GITHUB_STEP_SUMMARY
}
if (-not $Run) { return }

function Invoke-Checked([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed" }
}

$solution = Join-Path $root 'src/App.slnx'
if ($everything) {
    Invoke-Checked 'dotnet test' { dotnet test $solution }
    $npm = [Collections.Generic.SortedSet[string]]::new([string[]]@(
        Get-ChildItem (Join-Path $root 'packages') -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'package.json') } |
            ForEach-Object Name))
} elseif ($testProjects.Count -gt 0) {
    Invoke-Checked 'dotnet build' { dotnet build $solution }
    foreach ($t in $testProjects) {
        $csproj = Join-Path $root "$($projects[$t].Dir)/$t.csproj"
        if ($t -eq 'Integration.Tests') {
            $filter = ($areas | ForEach-Object { "FullyQualifiedName~Integration.Tests.Tests.$_." }) -join '|'
            Invoke-Checked $t { dotnet test $csproj --no-build --filter $filter }
        } else {
            Invoke-Checked $t { dotnet test $csproj --no-build }
        }
    }
}
foreach ($n in $npm) {
    Push-Location (Join-Path $root "packages/$n")
    try {
        Invoke-Checked "npm ci ($n)" { npm ci }
        if (Test-Path node_modules/@vitest/browser-playwright) { Invoke-Checked 'playwright install' { npx playwright install --with-deps chromium } }
        Invoke-Checked "npm test ($n)" { npm test }
    } finally { Pop-Location }
}
