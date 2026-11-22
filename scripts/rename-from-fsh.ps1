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
    The Hero and Fsh rules skip clients/: there "Hero" is a page section (PageHero) and the
    UI's names change with framework-ui.

    Wire names stay: metric and activity names (fsh.cache.*, FSH.Caching), headers (X-FSH-App),
    the JWT issuer and audience, localStorage keys, CSS classes, the Data Protection application
    name and protector purposes (renaming those would make stored keys and secrets unreadable).
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

# Files never rewritten: the upstream log quotes upstream's names, and the licence keeps
# fullstackhero's MIT notice.
$Skip = @('docs/upstream.md', 'LICENSE', 'scripts/rename-from-fsh.ps1')

function Convert-Text([string] $text, [bool] $inClients) {
    $text = $text -creplace 'FSH\.Framework', 'Trislab.Framework'
    $text = $text -creplace 'FSH\.Modules', 'Trislab.Modules'
    $text = $text -creplace 'FSH\.\*', 'Trislab.*'
    $text = $text -creplace '@fullstackhero/admin', '@trislabcompany/framework-admin'
    $text = $text -creplace '@fullstackhero/dashboard', '@trislabcompany/framework-dashboard'
    if (-not $inClients) {
        $text = $text -creplace '\b(Add|Use|Map|Configure)Hero(?=[A-Z])', '${1}Trislab'
        $text = $text -creplace '(?<![A-Za-z])Hero(?=[A-Z])', 'Trislab'
        $text = $text -creplace 'Fsh(?=[A-Z])', 'Trislab'
    }
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
    $left = git grep -I -c -E 'FSH\.(Framework|Modules)' -- . ':!docs/upstream.md' ':!scripts/rename-from-fsh.ps1'
    if ($left) { Write-Output "still named FSH: $left" }
}
finally { Pop-Location }
