#requires -Version 5
<#
.SYNOPSIS
    Windows (PowerShell) port of the `just rename` recipe.
.DESCRIPTION
    Rewrites BOTH the PascalCase token (Corbel -> NewName) and the lowercase token (corbel -> newname)
    inside text files, then renames matching files and folders. Mirrors the bash recipe in the justfile so
    Windows users without a Unix shell can run `just rename Acme`. Use a simple alphanumeric name; review the
    result with `git diff`.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true, Position = 0)] [string] $NewName)

$ErrorActionPreference = 'Stop'

$old = 'Corbel'
$oldLower = 'corbel'
$new = $NewName
$newLower = $NewName.ToLowerInvariant()

if ($new -ceq $old) {
    Write-Host "Project is already named '$old'. Nothing to do."
    exit 0
}

# Resolve the repo root (two levels up from eng/windows) so the script works regardless of the caller's cwd.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

Write-Host "Renaming '$old' -> '$new' (and '$oldLower' -> '$newLower') ..."

$skipDirs = @('.git', 'bin', 'obj', 'node_modules', 'dist', '.idea', '.vs')
$oldPattern = [regex]::Escape($old)
$oldLowerPattern = [regex]::Escape($oldLower)

function Test-Skipped {
    param([string] $FullPath)
    $relative = $FullPath.Substring($repoRoot.Length)
    foreach ($dir in $skipDirs) {
        $escaped = [regex]::Escape($dir)
        if ($relative -match "[\\/]$escaped([\\/]|$)") { return $true }
    }
    return $false
}

# Heuristic match for `grep -I`: a NUL byte in the first 8 KB means "treat as binary, skip".
function Test-Binary {
    param([byte[]] $Bytes)
    $limit = [Math]::Min($Bytes.Length, 8000)
    for ($i = 0; $i -lt $limit; $i++) {
        if ($Bytes[$i] -eq 0) { return $true }
    }
    return $false
}

# 1) Rewrite both tokens inside text files, preserving each file's existing UTF-8 BOM state and line endings.
Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Force | ForEach-Object {
    if (Test-Skipped $_.FullName) { return }
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    if ($bytes.Length -eq 0) { return }
    if (Test-Binary $bytes) { return }

    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $start = if ($hasBom) { 3 } else { 0 }
    $text = [System.Text.Encoding]::UTF8.GetString($bytes, $start, $bytes.Length - $start)

    if ($text -cmatch $oldPattern -or $text -cmatch $oldLowerPattern) {
        $updated = $text -creplace $oldPattern, $new -creplace $oldLowerPattern, $newLower
        $encoding = New-Object System.Text.UTF8Encoding($hasBom)
        [System.IO.File]::WriteAllText($_.FullName, $updated, $encoding)
    }
}

# 2) Rename files and folders whose name contains either token, deepest paths first so children move before
#    their parents.
Get-ChildItem -LiteralPath $repoRoot -Recurse -Force |
    Where-Object { -not (Test-Skipped $_.FullName) } |
    Where-Object { $_.Name -cmatch $oldPattern -or $_.Name -cmatch $oldLowerPattern } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $newLeaf = $_.Name -creplace $oldPattern, $new -creplace $oldLowerPattern, $newLower
        if ($newLeaf -cne $_.Name) {
            Rename-Item -LiteralPath $_.FullName -NewName $newLeaf
        }
    }

Write-Host "Done. Review with 'git diff', then run: just bootstrap; dotnet build; just test"
