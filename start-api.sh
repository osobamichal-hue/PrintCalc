#!/usr/bin/env bash
# Spustí PrintCalc.Api (production build) — voláno z PM2.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

DLL="$ROOT/src/PrintCalc.Api/bin/Release/net8.0/PrintCalc.Api.dll"
if [[ ! -f "$DLL" ]]; then
  echo "Chybí $DLL — spusťte: dotnet build src/PrintCalc.Api/PrintCalc.Api.csproj -c Release" >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Chybí dotnet v PATH (PM2 nenačítá shell profil)." >&2
  echo "Přidejte do ~/.bashrc: export PATH=\"\$PATH:\$HOME/.dotnet\"" >&2
  exit 1
fi

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5281}"

# Výchozí stejná cesta jako API bez ConnectionStrings (Linux: ~/.local/share/PrintCalc).
# Přepište PRINTCALC_DATA_DIR jen pokud chcete DB jinde (např. ~/PrintCalc/data).
if [[ -n "${PRINTCALC_DATA_DIR:-}" ]]; then
  DATA_DIR="$PRINTCALC_DATA_DIR"
else
  DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/PrintCalc"
fi
mkdir -p "$DATA_DIR"
DB_FILE="${DATA_DIR}/printcalc.db"
export ConnectionStrings__Default="Data Source=${DB_FILE}"

# Jednorázová migrace: pokud existuje starší DB v ~/PrintCalc/data z předchozí verze start-api.sh
LEGACY_DB="$ROOT/data/printcalc.db"
if [[ -f "$LEGACY_DB" && ! -f "$DB_FILE" ]]; then
  cp "$LEGACY_DB" "$DB_FILE"
  echo "Obnovena DB zkopírováním z ${LEGACY_DB}"
fi

echo "PrintCalc API — ${ASPNETCORE_URLS} · DB ${DB_FILE}"
exec dotnet "$DLL"
