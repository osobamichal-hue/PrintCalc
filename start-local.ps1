# Spustí API + Next.js v jednom terminálu (doporučeno).
# Alternativa: otevře dvě okna PowerShell.
param(
    [switch]$SeparateWindows
)

$root = $PSScriptRoot

if (-not $SeparateWindows) {
    Set-Location $root
    Write-Host "PrintCalc – spouštím API i web (npm run dev)…" -ForegroundColor Cyan
    Write-Host "Web: http://localhost:3000" -ForegroundColor Green
    npm run dev
    exit $LASTEXITCODE
}

$api = Join-Path $root "src\PrintCalc.Api\PrintCalc.Api.csproj"
$web = Join-Path $root "web"

Start-Process pwsh -ArgumentList @(
    "-NoExit", "-NoLogo", "-Command",
    "Set-Location '$root'; Write-Host 'PrintCalc.Api' -ForegroundColor Cyan; dotnet run --project `"$api`" --launch-profile http"
)

Start-Sleep -Seconds 3

Start-Process pwsh -ArgumentList @(
    "-NoExit", "-NoLogo", "-Command",
    "Set-Location '$web'; Write-Host 'Next.js (web)' -ForegroundColor Cyan; npm run dev"
)

Write-Host "Spuštěna dvě okna. Web: http://localhost:3000" -ForegroundColor Green
