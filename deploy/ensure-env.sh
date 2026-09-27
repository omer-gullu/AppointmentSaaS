#!/usr/bin/env bash
# Creates /opt/appointmentsaas/{webui,api}.env from repo templates when missing.
# Never overwrites existing secrets. WebUI ApiBaseUrl is always forced to loopback.
set -euo pipefail

ROLE="${1:-}"
if [[ "$ROLE" != "webui" && "$ROLE" != "api" ]]; then
  echo "usage: $0 webui|api [path-to-env.example]" >&2
  exit 2
fi

DIR=/opt/appointmentsaas
ENV_FILE="$DIR/${ROLE}.env"
EXAMPLE_SRC="${2:-}"
LOOPBACK_API='ApiBaseUrl=http://127.0.0.1:5294'

mkdir -p "$DIR"

if [[ -n "$EXAMPLE_SRC" && -f "$EXAMPLE_SRC" && ! "$EXAMPLE_SRC" -ef "$DIR/${ROLE}.env.example" ]]; then
  install -m 644 "$EXAMPLE_SRC" "$DIR/${ROLE}.env.example"
fi

if [[ ! -f "$ENV_FILE" ]]; then
  if [[ -f "$DIR/${ROLE}.env.example" ]]; then
    install -m 600 "$DIR/${ROLE}.env.example" "$ENV_FILE"
    echo "${ROLE}.env created from example — fill CHANGE_ME values before serving traffic"
  else
    echo "${ROLE}.env missing and no example present — writing minimal file"
    if [[ "$ROLE" == "webui" ]]; then
      printf '%s\n' 'ASPNETCORE_ENVIRONMENT=Production' "$LOOPBACK_API" > "$ENV_FILE"
    else
      printf '%s\n' 'ASPNETCORE_ENVIRONMENT=Production' > "$ENV_FILE"
    fi
    chmod 600 "$ENV_FILE"
  fi
fi

if [[ "$ROLE" == "webui" ]]; then
  current="$(grep -E '^[[:space:]]*ApiBaseUrl=' "$ENV_FILE" | head -n1 || true)"
  if [[ "$current" != "$LOOPBACK_API" ]]; then
    bak="$ENV_FILE.bak.$(date +%Y%m%d%H%M%S)"
    cp -a "$ENV_FILE" "$bak"
    tmp="$(mktemp)"
    awk -v target="$LOOPBACK_API" '
      BEGIN { found = 0 }
      /^[[:space:]]*ApiBaseUrl=/ {
        print target
        found = 1
        next
      }
      { print }
      END { if (!found) print target }
    ' "$ENV_FILE" > "$tmp"
    cat "$tmp" > "$ENV_FILE"
    rm -f "$tmp"
    echo "webui.env ApiBaseUrl normalized to loopback"
  else
    echo "webui.env ApiBaseUrl already loopback"
  fi
fi

# WebUI OAuth and API token refresh must use the same Google client.
# Copy Google__* from webui.env (preferred) or api.env into a shared file.
GOOGLE_ENV="$DIR/google.env"
SOURCE=""
if grep -qE '^[[:space:]]*Google__ClientSecret=' "$DIR/webui.env" 2>/dev/null; then
  SOURCE="$DIR/webui.env"
elif grep -qE '^[[:space:]]*Google__ClientSecret=' "$DIR/api.env" 2>/dev/null; then
  SOURCE="$DIR/api.env"
fi
if [[ -n "$SOURCE" ]]; then
  tmp="$(mktemp)"
  grep -E '^[[:space:]]*Google__' "$SOURCE" > "$tmp" || true
  if [[ -s "$tmp" ]]; then
    chmod 600 "$tmp"
    chown www-data:www-data "$tmp" 2>/dev/null || true
    mv "$tmp" "$GOOGLE_ENV"
    echo "google.env synced from $(basename "$SOURCE")"
  else
    rm -f "$tmp"
  fi
else
  echo "google.env skipped — neither webui.env nor api.env has Google__ClientSecret"
fi
