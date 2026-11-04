#!/usr/bin/env bash
# Golden-path smoke: Flow → Render → file (required) and optionally → Mail.
# Run from suite root after `docker compose up --build -d` (or let this script start it).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

FLOW_URL="${FLOW_URL:-http://127.0.0.1:5077}"
RENDER_URL="${RENDER_URL:-http://127.0.0.1:5066}"
MAIL_URL="${MAIL_URL:-http://127.0.0.1:5026}"
FLOW_API_KEY="${FLOW_API_KEY:-dev-flow-key}"
RENDER_API_KEY="${RENDER_API_KEY:-dev-local-key}"
MAIL_TO="${MAIL_TO:-compose-e2e@example.com}"
POLL_SECONDS="${POLL_SECONDS:-90}"
START_COMPOSE="${START_COMPOSE:-1}"
RUN_MAIL="${RUN_MAIL:-1}"

# Logs must go to stderr — stdout is used for command substitutions (e.g. API keys).
log() { printf '%s\n' "$*" >&2; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

need_cmd() { command -v "$1" >/dev/null 2>&1 || die "missing required command: $1"; }

need_cmd curl
need_cmd docker
need_cmd python3

compose() {
  if docker compose version >/dev/null 2>&1; then
    docker compose "$@"
  elif command -v docker-compose >/dev/null 2>&1; then
    docker-compose "$@"
  else
    die "need Docker Compose v2 plugin (`docker compose`) or `docker-compose`"
  fi
}

wait_http() {
  local url="$1" name="$2" n=0
  while (( n < 60 )); do
    if curl -sf -o /dev/null "$url"; then
      log "ok  $name ($url)"
      return 0
    fi
    # Mail /health may be 503 when SMTP is unreachable — treat any HTTP response as up
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

json_get() {
  # usage: json_get '<json>' 'python expr using data'
  local json="$1" expr="$2"
  JSON="$json" python3 -c "import json,os; data=json.loads(os.environ['JSON']); print($expr)"
}

poll_job() {
  local job_id="$1" label="$2"
  local deadline=$((SECONDS + POLL_SECONDS))
  local status="" body=""
  while (( SECONDS < deadline )); do
    body="$(curl -sf -H "X-OxyKit-Flow-Key: $FLOW_API_KEY" "$FLOW_URL/api/v1/jobs/$job_id")" \
      || die "GET job $job_id failed"
    status="$(json_get "$body" 'data["status"]')"
    log "  $label status=$status"
    case "$status" in
      Completed) return 0 ;;
      DeadLettered) die "$label dead-lettered: $body" ;;
    esac
    sleep 2
  done
  die "$label timed out after ${POLL_SECONDS}s (last=$status)"
}

ensure_env() {
  if [[ ! -f .env ]]; then
    log "creating .env for compose e2e"
    cat >.env <<'EOF'
RENDER_API_KEY=dev-local-key
FLOW_API_KEY=dev-flow-key
FLOW_MAIL_API_KEY=
DatabaseProvider=SQLite
Jwt__Key=compose-e2e-jwt-signing-key-32chars-min
Jwt__Issuer=OxyKit.Mail
Jwt__Audience=OxyKit.Mail
SmtpSettings__Host=smtp.example.com
SmtpSettings__Port=587
SmtpSettings__Username=e2e
SmtpSettings__Password=e2e
SmtpSettings__EnableSsl=true
SmtpSettings__AuthMode=Password
EOF
  fi
}

create_file_job() {
  local idem="compose-e2e-file-$(date +%s)"
  curl -sf -X POST "$FLOW_URL/api/v1/jobs/" \
    -H "X-OxyKit-Flow-Key: $FLOW_API_KEY" \
    -H 'Content-Type: application/json' \
    -H "Idempotency-Key: $idem" \
    -d '{
      "name": "compose-e2e-file",
      "render": {
        "input": "table",
        "output": "html",
        "body": {
          "sheets": [{
            "name": "Orders",
            "headers": ["Id", "Customer"],
            "rows": [[1, "Acme"], [2, "Beta"]]
          }]
        }
      },
      "outputs": [{
        "provider": "file",
        "destination": "output/compose_e2e.html"
      }]
    }'
}

create_mail_job() {
  local idem="compose-e2e-mail-$(date +%s)"
  local to_json
  to_json="$(MAIL_TO="$MAIL_TO" python3 -c 'import json,os; print(json.dumps(os.environ["MAIL_TO"]))')"
  curl -sf -X POST "$FLOW_URL/api/v1/jobs/" \
    -H "X-OxyKit-Flow-Key: $FLOW_API_KEY" \
    -H 'Content-Type: application/json' \
    -H "Idempotency-Key: $idem" \
    -d "{
      \"name\": \"compose-e2e-mail\",
      \"render\": {
        \"input\": \"table\",
        \"output\": \"html\",
        \"body\": {
          \"sheets\": [{
            \"name\": \"Orders\",
            \"headers\": [\"Id\", \"Customer\"],
            \"rows\": [[1, \"Acme\"], [2, \"Beta\"]]
          }]
        }
      },
      \"outputs\": [{
        \"provider\": \"mail\",
        \"destination\": ${to_json},
        \"subject\": \"OxyKit compose e2e\",
        \"body\": \"Attached report from compose e2e.\"
      }]
    }"
}

# --- main ---

ensure_env

if [[ "$START_COMPOSE" == "1" ]]; then
  log "starting compose stack..."
  compose up --build -d
fi

wait_http "$RENDER_URL/health" render
wait_http "$MAIL_URL/health" mail
wait_http "$FLOW_URL/health" flow

# sanity: Render accepts key
curl -sf -H "X-OxyKit-Render-Key: $RENDER_API_KEY" "$RENDER_URL/health" >/dev/null \
  || die "Render health with API key failed"

file_body="$(create_file_job)" || die "create file job failed"
file_id="$(json_get "$file_body" 'data["id"]')"
log "file job id=$file_id"
poll_job "$file_id" file

if [[ "$RUN_MAIL" == "1" ]]; then
  MAIL_URL="$MAIL_URL" FLOW_URL="$FLOW_URL" RECREATE_FLOW=1 KEY_LABEL=compose-e2e \
    "$ROOT/scripts/compose-bootstrap-mail.sh" \
    || die "compose-bootstrap-mail.sh failed (fresh mail-data volume or set RUN_MAIL=0)"

  mail_body="$(create_mail_job)" || die "create mail job failed"
  mail_id="$(json_get "$mail_body" 'data["id"]')"
  log "mail job id=$mail_id"
  poll_job "$mail_id" mail
fi

log "compose e2e passed"
