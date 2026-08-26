#!/usr/bin/env bash

set -euo pipefail

dotnet tool restore
dotnet restore mock-interviews.sln
dotnet build mock-interviews.sln --no-restore

if [[ "${CONDUCTOR_IS_LOCAL:-0}" == "1" ]]; then
    dotnet dev-certs https --trust
fi
