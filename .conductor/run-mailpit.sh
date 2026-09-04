#!/usr/bin/env bash

set -euo pipefail

if [[ -z "${CONDUCTOR_PORT:-}" ]]; then
    echo "CONDUCTOR_PORT is not set; this script must run in a local Conductor workspace." >&2
    exit 1
fi

if ! command -v mailpit >/dev/null 2>&1; then
    echo "Mailpit is not installed. Run: brew install mailpit" >&2
    exit 1
fi

workspace_path="${CONDUCTOR_WORKSPACE_PATH:-$(pwd)}"
workspace_name="${CONDUCTOR_WORKSPACE_NAME:-mock-interviews}"
ui_port=$((CONDUCTOR_PORT + 1))
smtp_port=$((CONDUCTOR_PORT + 2))
database_path="${workspace_path}/.context/mailpit.db"

mkdir -p "$(dirname "${database_path}")"
echo "Mailpit UI: http://127.0.0.1:${ui_port}"
echo "Mailpit SMTP: 127.0.0.1:${smtp_port}"

exec mailpit \
    --listen "127.0.0.1:${ui_port}" \
    --smtp "127.0.0.1:${smtp_port}" \
    --label "${workspace_name}" \
    --database "${database_path}" \
    --max 500
