#requires -Version 5
<#
.SYNOPSIS
    Windows (PowerShell) port of the `just bootstrap` recipe.
.DESCRIPTION
    One-time local setup: trust the dev HTTPS cert, install the repo-root dev tooling (husky pre-commit hook),
    put a random JWT signing key + a dev admin password into user-secrets (no secret ships in the repo), and
    create .env from .env.example. Mirrors the bash recipe so it runs without a Unix shell. `dotnet tool
    restore` is handled by the recipe's `tools` dependency.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
$api = 'src/Corbel.Api'

# Generate a cryptographically-random base64 string (replaces `openssl rand -base64`, which Windows lacks).
function New-RandomBase64 {
    param([int] $ByteCount)
    $buffer = New-Object byte[] $ByteCount
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try { $rng.GetBytes($buffer) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($buffer)
}

dotnet dev-certs https --trust

# Non-fatal: wire up the Biome pre-commit hook (husky). CI enforces the same lint/format checks regardless, so
# a missing pnpm shouldn't block setup.
try {
    pnpm install
} catch {
    Write-Warning "pnpm install failed; skipping pre-commit hook setup (CI still enforces lint/format). $_"
}

$secrets = dotnet user-secrets list --project $api 2>$null

if ($secrets -match '^Jwt:SigningKey') {
    Write-Host "Jwt:SigningKey already set in user-secrets; leaving it."
} else {
    dotnet user-secrets set "Jwt:SigningKey" (New-RandomBase64 48) --project $api | Out-Null
    Write-Host "Generated a new Jwt:SigningKey in user-secrets for $api."
}

if ($secrets -match '^Seed:AdminPassword') {
    Write-Host "Seed:AdminPassword already set in user-secrets; leaving it."
} else {
    $password = (New-RandomBase64 24) + "Aa1!"
    dotnet user-secrets set "Seed:AdminPassword" $password --project $api | Out-Null
    Write-Host "Dev admin will be seeded on first run - login: admin@corbel.local / $password"
}

if (Test-Path .env) {
    Write-Host ".env already exists; leaving it."
} else {
    Copy-Item .env.example .env
    Write-Host "Created .env from .env.example - edit it before 'just up'."
}
