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

if [[ "$ROLE" != "webui" ]]; then
  exit 0
fi

current="$(grep -E '^[[:space:]]*ApiBaseUrl=' "$ENV_FILE" | head -n1 || true)"
if [[ "$current" == "$LOOPBACK_API" ]]; then
  echo "webui.env ApiBaseUrl already loopback"
  exit 0
fi

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
