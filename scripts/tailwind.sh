#!/usr/bin/env bash

set -euo pipefail

readonly TAILWIND_VERSION="4.3.3"
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REPOSITORY_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly INPUT_FILE="${REPOSITORY_ROOT}/mock-interviews/Styles/tailwind.css"
readonly OUTPUT_FILE="${REPOSITORY_ROOT}/mock-interviews/wwwroot/css/tailwind.css"

usage() {
    echo "Usage: $0 {install|build|watch|version}" >&2
}

resolve_asset() {
    local os architecture
    os="$(uname -s)"
    architecture="$(uname -m)"

    case "${os}/${architecture}" in
        Darwin/arm64)
            TAILWIND_ASSET="tailwindcss-macos-arm64"
            TAILWIND_SHA256="cdf646702987a743464dff4d9c60fd4480d1c1e73dd819a9a67f1078815dce9d"
            ;;
        Darwin/x86_64)
            TAILWIND_ASSET="tailwindcss-macos-x64"
            TAILWIND_SHA256="7922e0953f2110c05976e3bf58f14e643d90427575e766b7d433f5f80cbee7e1"
            ;;
        Linux/aarch64|Linux/arm64)
            TAILWIND_ASSET="tailwindcss-linux-arm64"
            TAILWIND_SHA256="55fd0b241214eff3de1e8ee4f22796662f2d2e7a49bcfca7477cfd0bac398195"
            ;;
        Linux/x86_64|Linux/amd64)
            TAILWIND_ASSET="tailwindcss-linux-x64"
            TAILWIND_SHA256="dc61b3ac6b8c9ca874c0cc4c57b2409791a64c5540404ca5f5367360babc313a"
            ;;
        *)
            echo "Unsupported Tailwind CLI platform: ${os}/${architecture}" >&2
            exit 1
            ;;
    esac
}

sha256_file() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | awk '{ print $1 }'
    elif command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{ print $1 }'
    else
        echo "A SHA-256 utility (shasum or sha256sum) is required." >&2
        exit 1
    fi
}

resolve_cache_root() {
    if [[ -n "${TAILWIND_CACHE_DIR:-}" ]]; then
        printf '%s\n' "${TAILWIND_CACHE_DIR}"
    elif [[ -n "${XDG_CACHE_HOME:-}" ]]; then
        printf '%s\n' "${XDG_CACHE_HOME}/mock-interviews"
    elif [[ -n "${HOME:-}" ]]; then
        printf '%s\n' "${HOME}/.cache/mock-interviews"
    else
        printf '%s\n' "${REPOSITORY_ROOT}/.tools"
    fi
}

install_tailwind() {
    resolve_asset

    local cache_root version_directory executable actual_sha temporary_file download_url
    cache_root="$(resolve_cache_root)"
    version_directory="${cache_root}/tailwindcss/${TAILWIND_VERSION}"
    executable="${version_directory}/${TAILWIND_ASSET}"

    if [[ -x "${executable}" ]]; then
        actual_sha="$(sha256_file "${executable}")"
        if [[ "${actual_sha}" == "${TAILWIND_SHA256}" ]]; then
            printf '%s\n' "${executable}"
            return
        fi

        echo "Cached Tailwind CLI checksum does not match; downloading a verified copy." >&2
    fi

    mkdir -p "${version_directory}"
    temporary_file="$(mktemp "${version_directory}/download.XXXXXX")"
    download_url="https://github.com/tailwindlabs/tailwindcss/releases/download/v${TAILWIND_VERSION}/${TAILWIND_ASSET}"

    curl --fail --location --silent --show-error --retry 3 \
        --output "${temporary_file}" \
        "${download_url}"

    actual_sha="$(sha256_file "${temporary_file}")"
    if [[ "${actual_sha}" != "${TAILWIND_SHA256}" ]]; then
        echo "Tailwind CLI checksum verification failed." >&2
        rm -f "${temporary_file}"
        exit 1
    fi

    chmod 0755 "${temporary_file}"
    mv -f "${temporary_file}" "${executable}"
    printf '%s\n' "${executable}"
}

command="${1:-build}"

case "${command}" in
    install)
        install_tailwind >/dev/null
        echo "Tailwind CSS CLI v${TAILWIND_VERSION} is installed."
        ;;
    build)
        tailwind_executable="$(install_tailwind)"
        mkdir -p "$(dirname "${OUTPUT_FILE}")"
        "${tailwind_executable}" \
            --input "${INPUT_FILE}" \
            --output "${OUTPUT_FILE}" \
            --minify
        ;;
    watch)
        tailwind_executable="$(install_tailwind)"
        mkdir -p "$(dirname "${OUTPUT_FILE}")"
        exec "${tailwind_executable}" \
            --input "${INPUT_FILE}" \
            --output "${OUTPUT_FILE}" \
            --watch
        ;;
    version)
        echo "${TAILWIND_VERSION}"
        ;;
    *)
        usage
        exit 2
        ;;
esac
