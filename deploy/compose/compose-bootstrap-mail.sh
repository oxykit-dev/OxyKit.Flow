#!/usr/bin/env bash
# Mint a Mail Sender/Admin API key and wire FLOW_MAIL_API_KEY into suite-root .env,
# then recreate the Flow container so mail output works.
# Prerequisites: Mail (and usually the full compose stack) already up.
# From suite root: ./scripts/compose-bootstrap-mail.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MAIL_URL="${MAIL_URL:-http://127.0.0.1:5026}"
FLOW_URL="${FLOW_URL:-http://127.0.0.1:5077}"
RECREATE_FLOW="${RECREATE_FLOW:-1}"
ADMIN_USER="${ADMIN_USER:-e2e-admin}"
ADMIN_EMAIL="${ADMIN_EMAIL:-e2e-admin@example.com}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-OxYk1t-Compose-E2E-9f3a!}"
KEY_LABEL="${KEY_LABEL:-compose-bootstrap}"

log() { printf '%s\n' "$*" >&2; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

need_cmd() { command -v "$1" >/dev/null 2>&1 || die "missing required command: $1"; }

need_cmd curl
need_cmd python3
need_cmd docker

compose() {
  if docker compose version >/dev/null 2>&1; then
    docker compose "$@"
  elif command -v docker-compose >/dev/null 2>&1; then
    docker-compose "$@"
  else
    die "need Docker Compose v2 plugin (`docker compose`) or `docker-compose`"
  fi
}

json_get() {
  local json="$1" expr="$2"
  JSON="$json" python3 -c "import json,os; data=json.loads(os.environ['JSON']); print($expr)"
}

wait_http() {
  local url="$1" name="$2" n=0
  while (( n < 60 )); do
    if curl -sf -o /dev/null "$url"; then
      log "ok  $name ($url)"
      return 0
    fi
    if [[ "$name" == "mail" ]]; then
      local code
      code="$(curl -s -o /dev/null -w '%{http_code}' "$url" || true)"
      if [[ "$code" =~ ^[12345][0-9][0-9]$ ]]; then
        log "ok  $name ($url) http=$code"
        return 0
      fi
    fi
    sleep 2
    n=$((n + 1))
  done
  die "timeout waiting for $name at $url"
}

bootstrap_mail_key() {
  local needs login_body token key_body key
  needs="$(curl -sf "$MAIL_URL/api/v1/setup/needs-admin")" || die "needs-admin failed"
  if [[ "$(json_get "$needs" 'str(data["needsAdmin"]).lower()')" == "true" ]]; then
    curl -sf -X POST "$MAIL_URL/api/v1/setup/create-admin" \
      -H 'Content-Type: application/json' \
      -d "{\"userName\":\"$ADMIN_USER\",\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
      >/dev/null || die "create-admin failed"
    log "ok  mail admin created ($ADMIN_USER)"
  else
    log "ok  mail admin already exists"
  fi

  login_body="$(curl -sf -X POST "$MAIL_URL/api/v1/auth/login" \
    -H 'Content-Type: application/json' \
    -d "{\"identifier\":\"$ADMIN_USER\",\"password\":\"$ADMIN_PASSWORD\",\"deviceId\":\"compose-bootstrap\"}")" \
    || die "mail login failed (fresh mail-data volume, or set ADMIN_USER/ADMIN_PASSWORD to match existing admin)"
  token="$(json_get "$login_body" 'data["token"]')"
  [[ -n "$token" ]] || die "login returned empty token"

  key_body="$(curl -sf -X POST "$MAIL_URL/api/v1/apikeys" \
    -H "Authorization: Bearer $token" \
    -H 'Content-Type: application/json' \
    -d "{\"label\":\"$KEY_LABEL\"}")" \
    || die "create apikey failed"
  key="$(json_get "$key_body" 'data["plainApiKey"]')"
  [[ -n "$key" ]] || die "create apikey returned empty plainApiKey"
  [[ "$key" == *.* && ${#key} -gt 40 ]] || die "create apikey returned unexpected key shape"
  printf '%s' "$key"
}

write_env_key() {
  local key="$1"
  MAIL_KEY="$key" python3 - <<'PY'
import os, pathlib, re
key = os.environ["MAIL_KEY"]
quoted = '"' + key.replace("\\", "\\\\").replace('"', '\\"') + '"'
path = pathlib.Path(".env")
text = path.read_text() if path.exists() else ""
line = f"FLOW_MAIL_API_KEY={quoted}"
if re.search(r"^FLOW_MAIL_API_KEY=", text, flags=re.M):
    text = re.sub(r"^FLOW_MAIL_API_KEY=.*$", line, count=1, flags=re.M)
else:
    text = text.rstrip() + "\n" + line + "\n"
path.write_text(text)
PY
}

# --- main ---

if [[ ! -f .env ]]; then
  log "no .env — copying from .env.example"
  [[ -f .env.example ]] || die "missing .env and .env.example"
  cp .env.example .env
fi

wait_http "$MAIL_URL/health" mail

mail_key="$(bootstrap_mail_key)"
log "ok  mail API key minted (not printed)"
write_env_key "$mail_key"
unset mail_key
log "ok  wrote FLOW_MAIL_API_KEY to .env"

if [[ "$RECREATE_FLOW" == "1" ]]; then
  compose up -d --force-recreate flow
  wait_http "$FLOW_URL/health" flow
  log "ok  flow recreated with Mail API key"
fi

log "compose mail bootstrap done"
