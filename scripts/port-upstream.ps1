<#
.SYNOPSIS
    Ports fullstackhero commits by patch: maps upstream's paths to ours, renames the names,
    and applies each patch with `git am -3`.

.DESCRIPTION
    The "by patch" way of docs/upstream.md, section 1, step 4, as one command. For each commit,
    oldest first:

      1. `git format-patch` the commit from a clone of fullstackhero's repository;
      2. map each file's path with docs/upstream.md, section 2 (.agents/rules -> .claude/rules, ...);
      3. leave out the files that have no place here, and say why:
           - paths removed here (deploy/, src/Tools/, templates/, ...): Not applicable;
           - the app (src/Host/, clients/, the sample modules Catalog, Tickets and Chat and their tests): it lives in
             tl-product-saas, so port it there;
           - upstream migrations: port the model change, then generate the migration (create-migration);
           - .agents/workflows/: port by hand into .claude/agents/ or the skills;
           - pt-BR resources and catalogs: add the Slovenian text to the sl-SI file by hand;
      4. rename the names with scripts/rename-from-fsh.ps1 -Patch;
      5. `git am -3` the patch, unless -NoApply.

    It stops at the first patch that does not apply cleanly. Resolve the conflicts, run
    `git am --continue`, then run this script again with the commits that are left.
    A commit whose files are all left out is not applied; mark its row Not applicable, or port the
    left-out part by hand.

    Run it from a branch of our main, with a clean working tree. The upstream clone must have the
    commits (git -C <clone> fetch origin).

.PARAMETER Commit
    One or more upstream commits, oldest first.

.PARAMETER Upstream
    The path of a local clone of https://github.com/fullstackhero/dotnet-starter-kit.

.PARAMETER NoApply
    Write the rewritten patches and stop: no `git am`. Use it to read a patch before applying it.

.PARAMETER OutDir
    Where the patches are written. Default: a new folder in the system temp folder.

.EXAMPLE
    pwsh scripts/port-upstream.ps1 -Upstream ../dotnet-starter-kit -Commit 2f5989b
    pwsh scripts/port-upstream.ps1 -Upstream ../dotnet-starter-kit -Commit 70e4758,2f5989b -NoApply
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string[]] $Commit,
    [Parameter(Mandatory)] [string] $Upstream,
    [switch] $NoApply,
    [string] $OutDir
)

$ErrorActionPreference = 'Stop'

# `pwsh -File` passes "a,b" as one string.
$Commit = @($Commit | ForEach-Object { $_ -split ',' } | Where-Object { $_ })

# Upstream paths with no place here (docs/upstream.md, section 2), and why.
$Dropped = @(
    @{ Pattern = '^src/Host/FSH\.Starter\.Migrations\.PostgreSQL/'; Reason = 'upstream migration: port the model change, then generate the migration here (create-migration)' }
    @{ Pattern = '^(src/Host/FSH\.Starter\.(Api|AppHost|DbMigrator)/|clients/|src/Modules/(Catalog|Tickets|Chat)/|src/Tests/(Catalog|Tickets|Chat)\.Tests/)'; Reason = 'the app lives in tl-product-saas: port the change there (Not applicable here)' }
    @{ Pattern = '^\.agents/workflows/'; Reason = 'agent workflow: port by hand into .claude/agents/ or the skills' }
    @{ Pattern = '(\.pt-BR\.resx$|/locales/pt-BR/)'; Reason = 'pt-BR: Slovenian replaces it here; add the Slovenian text to the matching sl-SI file by hand' }
    @{ Pattern = '^(deploy|src/Tools|templates|\.template\.config|superpowers|docs/superpowers)/'; Reason = 'removed here: Not applicable' }
    @{ Pattern = '^(GEMINI|README-template|CONTRIBUTING|SECURITY)\.md$'; Reason = 'removed here: Not applicable' }
)

# Upstream path -> our path (docs/upstream.md, section 2). The first match wins.
$Moved = @(
    @{ Pattern = '^src/FSH\.Starter\.slnx$'; Replace = 'src/App.slnx' }
    @{ Pattern = '^\.agents/rules/'; Replace = '.claude/rules/' }
)

function Get-DropReason([string] $path) {
    foreach ($d in $Dropped) { if ($path -match $d.Pattern) { return $d.Reason } }
    return $null
}

function Convert-Path([string] $path) {
    if ($path -eq '/dev/null') { return $path }
    foreach ($m in $Moved) {
        if ($path -match $m.Pattern) { return $path -replace $m.Pattern, $m.Replace }
    }
    return $path
}

# Rewrites one format-patch file: maps the paths in each file's header, and leaves out the files
# that have no place here. Returns the files kept and the files left out.
function Convert-Patch([string] $file) {
    $lines = [IO.File]::ReadAllText($file) -split "(?<=\n)"
    $out = [Text.StringBuilder]::new()
    $kept = [Collections.Generic.List[string]]::new()
    $left = [Collections.Generic.List[string]]::new()
    $skip = $false
    $inHeader = $false
    $seenDiff = $false
    foreach ($line in $lines) {
        # The commit message names the upstream commit (docs/upstream.md, section 1, step 4).
        if (-not $seenDiff -and $line -match '^Subject: \[PATCH[^\]]*\] (.*)$') {
            $line = "Subject: [PATCH] Sync fullstackhero ${script:Short}: $($Matches[1])`n"
        }
        if ($line -match '^diff --git a/(\S+) b/(\S+)') {
            $seenDiff = $true
            $a = $Matches[1]; $b = $Matches[2]
            $reason = Get-DropReason $a
            if (-not $reason) { $reason = Get-DropReason $b }
            $skip = [bool] $reason
            $inHeader = -not $skip
            if ($skip) { $left.Add("$a ($reason)"); continue }
            $kept.Add((Convert-Path $b))
            [void]$out.Append("diff --git a/$(Convert-Path $a) b/$(Convert-Path $b)`n")
            continue
        }
        if ($skip) { continue }
        if ($inHeader) {
            if ($line -match '^(@@|GIT binary patch)') {
                $inHeader = $false
            }
            elseif ($line -match '^(---|\+\+\+) (a/|b/)?(\S+)(\r?\n)?$') {
                $prefix = if ($Matches[2]) { $Matches[2] } else { '' }
                $line = "$($Matches[1]) $prefix$(Convert-Path $Matches[3])`n"
            }
            elseif ($line -match '^(rename|copy) (from|to) (\S+)') {
                $line = "$($Matches[1]) $($Matches[2]) $(Convert-Path $Matches[3])`n"
            }
        }
        [void]$out.Append($line)
    }
    [IO.File]::WriteAllText($file, $out.ToString())
    return @{ Kept = $kept; Left = $left }
}

$root = (git rev-parse --show-toplevel).Trim()
if (-not (Test-Path (Join-Path $Upstream '.git'))) { throw "Not a git clone: $Upstream" }
if (-not $NoApply -and (git -C $root status --porcelain)) { throw 'The working tree is not clean. Commit or stash first.' }
if (-not $OutDir) { $OutDir = Join-Path ([IO.Path]::GetTempPath()) "port-upstream-$(Get-Date -Format yyyyMMddHHmmss)" }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$rename = Join-Path $root 'scripts/rename-from-fsh.ps1'

$n = 0
foreach ($c in $Commit) {
    $n++
    $patch = (git -C $Upstream format-patch -1 $c --no-signature --start-number $n -o $OutDir).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $patch) { throw "format-patch failed for $c" }
    $script:Short = (git -C $Upstream rev-parse --short=7 $c).Trim()
    $result = Convert-Patch $patch
    $subject = (git -C $Upstream log -1 --format='%h %s' $c).Trim()
    Write-Output "$subject"
    Write-Output "  $($result.Kept.Count) file(s) kept, $($result.Left.Count) left out"
    foreach ($l in $result.Left) { Write-Output "  left out: $l" }

    if ($result.Kept.Count -eq 0) {
        Remove-Item $patch
        Write-Output '  not applied: no file of this commit has a place here'
        continue
    }
    & pwsh -NoProfile -File $rename -Patch $patch | ForEach-Object { "  $_" }
    if ($NoApply) { Write-Output "  patch: $patch"; continue }

    git -C $root am -3 $patch
    if ($LASTEXITCODE -ne 0) {
        $rest = @($Commit | Select-Object -Skip $n)
        Write-Output ''
        Write-Output "Stopped at $c. Resolve the conflicts, then: git am --continue"
        if ($rest) { Write-Output "Then run this script again with: -Commit $($rest -join ',')" }
        exit 1
    }
}
Write-Output "Patches in $OutDir"
