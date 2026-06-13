#!/usr/bin/env bash
# Spustí PrintCalc.Api + Next.js (production) v jednom terminálu.
# Použití: ./start-server.sh
# Volitelně: WEB_PORT=3001 ASPNETCORE_URLS=http://0.0.0.0:5281 ./start-server.sh

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5281}"
WEB_PORT="${WEB_PORT:-3001}"

if [[ ! -d node_modules/concurrently ]]; then
  echo "→ Instaluji npm závislosti v kořeni projektu…"
  npm install
fi

if [[ ! -d web/.next ]]; then
  echo "Chybí production build webu. Spusťte:"
  echo "  cd web && npm install && npm run build"
  exit 1
fi

echo "PrintCalc – spouštím API (${ASPNETCORE_URLS}) a web (port ${WEB_PORT})…"
echo "Web: http://$(hostname -I 2>/dev/null | awk '{print $1}'):${WEB_PORT}"

exec npx concurrently -k -n api,web -c cyan,magenta \
  "dotnet run --project src/PrintCalc.Api/PrintCalc.Api.csproj -c Release --no-launch-profile" \
  "wait-on -t 120000 http-get://127.0.0.1:5281/api/health && npm run start --prefix web -- -p ${WEB_PORT}"
