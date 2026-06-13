#!/usr/bin/env bash
# Počká na API a spustí Next.js (production).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

WEB_PORT="${WEB_PORT:-3001}"

if [[ ! -d web/.next ]]; then
  echo "Chybí web/.next — spusťte: cd web && npm run build"
  exit 1
fi

echo "Čekám na API http://127.0.0.1:5281/api/health …"
npx wait-on -t 120000 http-get://127.0.0.1:5281/api/health
echo "Spouštím Next.js na portu ${WEB_PORT} …"
exec npm run start --prefix web -- -p "${WEB_PORT}"
