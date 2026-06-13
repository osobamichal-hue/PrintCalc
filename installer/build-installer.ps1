param(
    [string]$Version = "1.1.1",
    [string]$InstallerName = "3DPrintCalc-Setup"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$AppProject = Join-Path $RepoRoot "src\PrintCalc.App\PrintCalc.App.csproj"
$PublishDir = Join-Path $RepoRoot "artifacts\publish\win-x64"
$InstallerDir = Join-Path $RepoRoot "artifacts\installer"
$IssPath = Join-Path $PSScriptRoot "PrintCalcSetup.iss"

Write-Host "==> Čištění artifacts..."
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $InstallerDir) { Remove-Item $InstallerDir -Recurse -Force }
New-Item -ItemType Directory -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Path $InstallerDir | Out-Null

Write-Host "==> Publish aplikace (self-contained, single-file)..."
dotnet publish $AppProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir

Write-Host "==> Kontrola publish obsahu (bez dat uzivatele)..."
$forbiddenPatterns = @("*.db", "*.db-*", "PrintCalc_Backup_*.zip", "backup-manifest.json", "appsettings-db.json")
foreach ($pattern in $forbiddenPatterns) {
    Get-ChildItem -Path $PublishDir -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "   Odebiram z publish: $($_.FullName)"
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
    }
}

$iscc = Get-Command "iscc" -ErrorAction SilentlyContinue
if (-not $iscc) {
    $fallbackIscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
    if (Test-Path $fallbackIscc) {
        $iscc = @{ Source = $fallbackIscc }
    }
}
if (-not $iscc) {
    Write-Warning "Inno Setup (iscc.exe) nebyl nalezen v PATH."
    Write-Warning "Aplikace je publikována v: $PublishDir"
    Write-Warning "Pro setup.exe nainstalujte Inno Setup a spusťte:"
    Write-Warning "  iscc `"$IssPath`" /DMyAppVersion=$Version"
    exit 0
}

Write-Host "==> Vytvářím setup.exe přes Inno Setup..."
& $iscc.Source $IssPath "/DMyAppVersion=$Version" "/DMyOutputBaseFilename=$InstallerName"

Write-Host "==> Hotovo. Výstup:"
Write-Host "    $InstallerDir\$InstallerName.exe"
