#!/usr/bin/env bash
# Nasazení PrintCalc na produkční server (Ubuntu + PM2).
#
# Použití:
#   ./deploy.sh              # git pull + build + restart PM2
#   ./deploy.sh --force      # git reset --hard origin/main před pull (zahodí lokální změny)
#   ./deploy.sh --setup      # první nasazení (pm2 start místo restart)
#   ./deploy.sh --no-pull    # přeskočí git pull (jen build + restart)
#
# Požadavky: git, dotnet 8, node/npm, pm2

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

API_URL="${PRINTCALC_API_HEALTH_URL:-http://127.0.0.1:5281/api/health}"
WEB_URL="${PRINTCALC_WEB_URL:-http://127.0.0.1:3001/}"
WEB_PORT="${WEB_PORT:-3001}"
GIT_BRANCH="${GIT_BRANCH:-main}"
API_WAIT_SEC="${API_WAIT_SEC:-120}"
WEB_WAIT_SEC="${WEB_WAIT_SEC:-90}"

FORCE_PULL=0
SKIP_PULL=0
PM2_SETUP=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --force) FORCE_PULL=1; shift ;;
    --no-pull) SKIP_PULL=1; shift ;;
    --setup) PM2_SETUP=1; shift ;;
    -h|--help)
      sed -n '2,10p' "$0" | sed 's/^# \?//'
      exit 0
      ;;
    *)
      echo "Neznámý argument: $1 (zkuste --help)" >&2
      exit 1
      ;;
  esac
done

log() { echo "→ $*"; }
ok() { echo "✓ $*"; }
fail() { echo "✗ $*" >&2; exit 1; }

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || fail "Chybí příkaz: $1"
}

wait_for_url() {
  local url="$1"
  local label="$2"
  local timeout="$3"
  local elapsed=0
  while (( elapsed < timeout )); do
    if curl -sf "$url" >/dev/null 2>&1; then
      ok "$label odpovídá ($url)"
      return 0
    fi
    if (( elapsed > 0 && elapsed % 10 == 0 )); then
      log "Stále čekám na $label… (${elapsed}s / ${timeout}s)"
    fi
    sleep 2
    elapsed=$((elapsed + 2))
  done
  echo "" >&2
  pm2 logs printcalc-api --lines 25 --nostream 2>/dev/null || true
  fail "$label neodpovídá do ${timeout}s ($url) — viz logy výše"
}

wait_for_port_free() {
  local port="$1"
  local elapsed=0
  while ss -tln 2>/dev/null | grep -q ":${port} "; do
    sleep 1
    elapsed=$((elapsed + 1))
    if (( elapsed % 5 == 0 )); then
      log "Port ${port} stále obsazen… (${elapsed}s)"
    fi
    if (( elapsed >= 45 )); then
      fail "Port ${port} je stále obsazen — zkuste: pm2 delete all && ss -tlnp | grep ${port}"
    fi
  done
}

pm2_stop_all() {
  log "Zastavuji PM2 služby…"
  pm2 stop printcalc-web printcalc-api 2>/dev/null || true
  sleep 2
  wait_for_port_free 5281
  wait_for_port_free 3001
}

pm2_start_fresh() {
  log "PM2 — čisté spuštění (API → web)…"
  pm2 delete printcalc printcalc-api printcalc-web 2>/dev/null || true
  sleep 2
  wait_for_port_free 5281
  wait_for_port_free 3001
  pm2 start ecosystem.config.cjs --only printcalc-api
  wait_for_url "$API_URL" "API" "$API_WAIT_SEC"
  pm2 start ecosystem.config.cjs --only printcalc-web
}

pm2_restart_services() {
  pm2_stop_all
  log "Spouštím API…"
  pm2 start printcalc-api --update-env 2>/dev/null || pm2 start ecosystem.config.cjs --only printcalc-api
  wait_for_url "$API_URL" "API" "$API_WAIT_SEC"
  log "Spouštím web…"
  pm2 start printcalc-web --update-env 2>/dev/null || pm2 start ecosystem.config.cjs --only printcalc-web
}

require_cmd git
require_cmd dotnet
require_cmd npm
require_cmd pm2
require_cmd curl

log "PrintCalc deploy — $ROOT"

# --- Git ---
if [[ "$SKIP_PULL" -eq 0 ]]; then
  log "Stahuji změny z git (větev $GIT_BRANCH)…"
  git fetch origin "$GIT_BRANCH"
  if [[ "$FORCE_PULL" -eq 1 ]]; then
    log "Reset na origin/$GIT_BRANCH (--force)"
    git reset --hard "origin/$GIT_BRANCH"
  fi
  git pull --ff-only origin "$GIT_BRANCH"
  ok "Git aktuální ($(git rev-parse --short HEAD))"
else
  log "Git pull přeskočen (--no-pull)"
fi

# --- Oprávnění skriptů ---
chmod +x start-web.sh start-server.sh deploy.sh 2>/dev/null || true

# --- API build ---
log "Build API (Release)…"
dotnet build src/PrintCalc.Api/PrintCalc.Api.csproj -c Release --nologo -v q
ok "API build hotov"

# --- Web build ---
log "Instalace a build webu…"
npm install --prefix web --no-fund --no-audit
npm run build --prefix web
ok "Web build hotov"

# --- Kořenové npm (wait-on pro start-web.sh) ---
if [[ ! -d node_modules/wait-on ]]; then
  log "Instalace npm závislostí v kořeni…"
  npm install --no-fund --no-audit
fi

# --- PM2 ---
if [[ "$PM2_SETUP" -eq 1 ]] || ! pm2 describe printcalc-api >/dev/null 2>&1; then
  pm2_start_fresh
elif ! pm2 describe printcalc-web >/dev/null 2>&1; then
  log "PM2 — chybí web, doplňuji…"
  wait_for_url "$API_URL" "API" "$API_WAIT_SEC"
  pm2 start ecosystem.config.cjs --only printcalc-web
else
  pm2_restart_services
fi

pm2 save
ok "PM2 uloženo"

# --- Ověření ---
log "Čekám na služby…"
wait_for_url "$API_URL" "API" "$API_WAIT_SEC"

web_code="$(curl -s -o /dev/null -w '%{http_code}' "$WEB_URL" || true)"
if [[ "$web_code" == "200" ]]; then
  ok "Web odpovídá HTTP 200 ($WEB_URL)"
else
  log "Web zatím neodpovídá 200 (kód: ${web_code:-?}), čekám…"
  elapsed=0
  while (( elapsed < WEB_WAIT_SEC )); do
    web_code="$(curl -s -o /dev/null -w '%{http_code}' "$WEB_URL" || true)"
    [[ "$web_code" == "200" ]] && break
    sleep 3
    elapsed=$((elapsed + 3))
  done
  [[ "$web_code" == "200" ]] || fail "Web neodpovídá HTTP 200 ($WEB_URL, kód: ${web_code:-?})"
  ok "Web odpovídá HTTP 200 ($WEB_URL)"
fi

echo ""
pm2 list
echo ""
ok "Deploy dokončen — $(git rev-parse --short HEAD)"
LAN_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
[[ -n "$LAN_IP" ]] && echo "   Web: http://${LAN_IP}:${WEB_PORT}"
