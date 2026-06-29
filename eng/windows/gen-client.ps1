#requires -Version 5
<#
.SYNOPSIS
    Windows (PowerShell) port of the `just gen-client` recipe.
.DESCRIPTION
    Boots the API (needs a reachable Postgres — start one first via `just dev` or `docker compose up -d
    postgres`), curls /openapi/v1.json into ./openapi.json, then runs the openapi-typescript generator over
    it. Mirrors the bash recipe so it runs without a Unix shell.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
$api = 'src/Corbel.Api'
$url = 'http://localhost:5229'

$env:ASPNETCORE_ENVIRONMENT = 'Development'
if (-not $env:Jwt__SigningKey) {
    $buffer = New-Object byte[] 48
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try { $rng.GetBytes($buffer) } finally { $rng.Dispose() }
    $env:Jwt__SigningKey = [Convert]::ToBase64String($buffer)
}

$log = Join-Path ([System.IO.Path]::GetTempPath()) 'corbel-gen-client.log'
Write-Host "Booting the API to dump its OpenAPI document..."
$proc = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $api, '--urls', $url) `
    -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err"

try {
    $fetched = $false
    for ($i = 0; $i -lt 90; $i++) {
        if ($proc.HasExited) {
            Write-Error "API exited early - see $log"
            exit 1
        }
        try {
            Invoke-WebRequest -Uri "$url/openapi/v1.json" -OutFile 'openapi.json' -UseBasicParsing -ErrorAction Stop
            Write-Host "Wrote openapi.json"
            $fetched = $true
            break
        } catch {
            Start-Sleep -Seconds 1
        }
    }

    if (-not $fetched -or -not (Test-Path 'openapi.json') -or (Get-Item 'openapi.json').Length -eq 0) {
        Write-Error "Failed to fetch openapi.json (is Postgres reachable?)"
        exit 1
    }

    pnpm -C web run gen:client
    Write-Host "Regenerated web/src/types/schema.d.ts from openapi.json - review with 'git diff'."
} finally {
    if (-not $proc.HasExited) {
        $proc.Kill()
    }
}
