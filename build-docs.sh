#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCK_DIR="${SCRIPT_DIR}/site/.lunet/.build-lock"

clean_docs_outputs() {
    find "${SCRIPT_DIR}/src" -path '*/obj/Release/*/Avalonia.Controls.TreeDataGrid.api.json' -delete
    rm -rf "${SCRIPT_DIR}/site/.lunet/build/cache/api/dotnet" \
           "${SCRIPT_DIR}/site/.lunet/build/www"
}

cd "${SCRIPT_DIR}"
while ! mkdir "${LOCK_DIR}" 2>/dev/null; do
    sleep 1
done

LUNET_LOG=""
cleanup() {
    if [ -n "${LUNET_LOG}" ] && [ -f "${LUNET_LOG}" ]; then
        rm -f "${LUNET_LOG}"
    fi

    rmdir "${LOCK_DIR}" 2>/dev/null || true
}

trap cleanup EXIT

dotnet tool restore
clean_docs_outputs

cd site
LUNET_LOG="$(mktemp)"

dotnet tool run lunet --stacktrace build 2>&1 | tee "${LUNET_LOG}"

if rg -n 'ERR lunet|Error while building api dotnet|Unable to select the api dotnet output' "${LUNET_LOG}" >/dev/null; then
    echo "Lunet reported API/site build errors."
    exit 1
fi
