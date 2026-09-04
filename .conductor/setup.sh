#!/usr/bin/env bash

set -euo pipefail

dotnet tool restore
dotnet restore mock-interviews.sln
./scripts/tailwind.sh build
dotnet build mock-interviews.sln --no-restore

if [[ "${CONDUCTOR_IS_LOCAL:-0}" == "1" ]]; then
    if ! command -v mailpit >/dev/null 2>&1; then
        if command -v brew >/dev/null 2>&1; then
            brew install mailpit
        else
            echo "Mailpit is required for local Conductor development. Install Homebrew, then run: brew install mailpit" >&2
            exit 1
        fi
    fi

    dotnet dev-certs https --trust
fi
