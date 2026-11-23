<#
.SYNOPSIS
    Renames fullstackhero's names to Trislab's, in this repository or in an upstream patch.

.DESCRIPTION
    The same rules run over the working tree (once, the rename PR) and over every upstream
    patch before `git am -3` (docs/upstream.md, section 1), so a port lands on renamed code.

    Code names change:
      FSH.Framework.*  -> Trislab.Framework.*      FSH.Modules.* -> Trislab.Modules.*
      FSH.*            -> Trislab.*  (the literal assembly glob)
      AddHero*, UseHero*, MapHero*, ConfigureHero*, Hero<Type> -> ...Trislab...
      Fsh<Type>, IFsh<Type>                        -> Trislab<Type>, ITrislab<Type>
      @fullstackhero/admin, @fullstackhero/dashboard -> @trislabcompany/framework-admin, -dashboard
      uploads/fshuser/ -> uploads/trislabuser/  (the upload folder follows the owner type's name)
    The Hero rules skip clients/: there "Hero" is a page section (PageHero).

    Wire and brand names change too, everywhere (renamed before v0.1.0, when no data existed yet):
      FSHCorsPolicy -> TrislabCorsPolicy      X-FSH-<Name>, x-fsh-<name> -> X-Trislab-<Name>, x-trislab-<name>
      FSH_<NAME>    -> TRISLAB_<NAME>         FSH (a word, e.g. FSH.Caching, "FSH API") -> Trislab
      Fsh<Type>     -> Trislab<Type>          fsh (a lowercase prefix: fsh.*, fsh-*, fsh_*, fshOptions) -> tl
    So metric and activity names, headers, the JWT issuer and audience, localStorage keys, CSS
    classes, database names and the Data Protection purposes are Trislab's. The fullstackhero
    wordmark, logo and links were removed by hand, not by a rule.
    Project files and folders keep upstream's layout, so patches apply by path.

.PARAMETER Patch
    A patch file (git format-patch) to rewrite in place. Without it, the working tree is renamed:
    file contents of every tracked text file, and files whose names carry a renamed type (git mv).

.EXAMPLE
    pwsh scripts/rename-from-fsh.ps1
    pwsh scripts/rename-from-fsh.ps1 -Patch 0001-upstream.patch
#>
[CmdletBinding()]
param([string] $Patch)

$ErrorActionPreference = 'Stop'

# Files never rewritten: the upstream log and the port scripts quote upstream's names and paths,
# and THIRD-PARTY-NOTICES.md keeps fullstackhero's MIT notice.
$Skip = @('docs/upstream.md', 'THIRD-PARTY-NOTICES.md', 'scripts/rename-from-fsh.ps1', 'scripts/port-upstream.ps1')

function Convert-Text([string] $text, [bool] $inClients) {
    $text = $text -creplace 'FSH\.Framework', 'Trislab.Framework'
    $text = $text -creplace 'FSH\.Modules', 'Trislab.Modules'
    $text = $text -creplace 'FSH\.\*', 'Trislab.*'
    $text = $text -creplace '@fullstackhero/admin', '@trislabcompany/framework-admin'
    $text = $text -creplace '@fullstackhero/dashboard', '@trislabcompany/framework-dashboard'
    if (-not $inClients) {
        $text = $text -creplace '\b(Add|Use|Map|Configure)Hero(?=[A-Z])', '${1}Trislab'
        $text = $text -creplace '(?<![A-Za-z])Hero(?=[A-Z])', 'Trislab'
        # Upload folders are named after the owner type (StorageUploadKeys): FshUser -> uploads/trislabuser/.
        $text = $text -creplace 'uploads/fshuser/', 'uploads/trislabuser/'
    }
    $text = $text -creplace '(?<![A-Za-z])Fsh(?=[A-Z])', 'Trislab'
    # Wire and brand names. The script's own name (rename-from-fsh) is kept.
    $text = $text -creplace 'FSHCorsPolicy', 'TrislabCorsPolicy'
    $text = $text -creplace '(?<![A-Za-z])X-FSH-', 'X-Trislab-'
    $text = $text -creplace '(?<![A-Za-z])x-fsh-', 'x-trislab-'
    $text = $text -creplace '(?<![A-Za-z])FSH_', 'TRISLAB_'
    $text = $text -creplace '(?<![A-Za-z])FSH(?![A-Za-z])', 'Trislab'
    $text = $text -creplace '(?<![A-Za-z]|rename-from-)fsh(?![a-z])', 'tl'
    return $text
}

function Test-Clients([string] $path) { return $path -like 'clients/*' }

if ($Patch) {
    # Rewrite each file's section of the patch with that file's rules; the header (the commit
    # message) gets the rules outside clients/.
    $lines = [IO.File]::ReadAllText((Resolve-Path $Patch)) -split "(?<=\n)"
    $out = [Text.StringBuilder]::new()
    $inClients = $false
    $changed = 0
    foreach ($line in $lines) {
        if ($line -match '^diff --git a/(\S+) b/') { $inClients = Test-Clients $Matches[1] }
        $new = Convert-Text $line $inClients
        if ($new -cne $line) { $changed++ }
        [void]$out.Append($new)
    }
    [IO.File]::WriteAllText((Resolve-Path $Patch), $out.ToString())
    Write-Output "rename-from-fsh: $changed line(s) renamed in $Patch"
    return
}

$root = (git rev-parse --show-toplevel).Trim()
Push-Location $root
try {
    $edited = 0
    foreach ($path in (git ls-files)) {
        if ($Skip -contains $path) { continue }
        $bytes = [IO.File]::ReadAllBytes($path)
        if ($bytes -contains 0) { continue }   # binary
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        $text = [IO.File]::ReadAllText($path)
        $new = Convert-Text $text (Test-Clients $path)
        if ($new -cne $text) {
            [IO.File]::WriteAllText($path, $new, [Text.UTF8Encoding]::new($hasBom))
            $edited++
        }
    }

    $moved = 0
    foreach ($path in (git ls-files)) {
        if ($Skip -contains $path -or (Test-Clients $path)) { continue }
        $dir = Split-Path $path -Parent
        $name = Split-Path $path -Leaf
        $newName = Convert-Text $name $false
        if ($newName -cne $name) {
            git mv -- $path (Join-Path $dir $newName).Replace('\', '/')
            $moved++
        }
    }
    Write-Output "rename-from-fsh: $edited file(s) edited, $moved file(s) renamed"
    $left = git grep -I -c -i -E 'fsh|fullstackhero' -- . ':!docs/upstream.md' ':!THIRD-PARTY-NOTICES.md' ':!scripts/*.ps1' ':!scripts/README.md'
    if ($left) { Write-Output "still named fullstackhero or FSH: $left" }
}
finally { Pop-Location }
