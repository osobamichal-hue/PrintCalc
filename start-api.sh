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

DATA_DIR="${PRINTCALC_DATA_DIR:-$ROOT/data}"
mkdir -p "$DATA_DIR"
export ConnectionStrings__Default="Data Source=${DATA_DIR}/printcalc.db"

echo "PrintCalc API — ${ASPNETCORE_URLS} · DB ${DATA_DIR}/printcalc.db"
exec dotnet "$DLL"
