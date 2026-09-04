#!/usr/bin/env bash

set -euo pipefail

if [[ -z "${CONDUCTOR_PORT:-}" ]]; then
    echo "CONDUCTOR_PORT is not set; this script must run in a local Conductor workspace." >&2
    exit 1
fi

export ASPNETCORE_ENVIRONMENT=Development
export Email__Provider=Smtp
export Email__Smtp__Host=127.0.0.1
export Email__Smtp__Port=$((CONDUCTOR_PORT + 2))
export Email__Smtp__UseTls=false

cd mock-interviews

exec dotnet watch run \
    --no-launch-profile \
    --urls "https://localhost:${CONDUCTOR_PORT}"
