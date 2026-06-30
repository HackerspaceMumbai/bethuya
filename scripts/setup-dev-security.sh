#!/usr/bin/env bash

set -euo pipefail

if ! git rev-parse --show-toplevel >/dev/null 2>&1; then
    echo "This script must be run inside a Git repository."
    exit 1
fi

repo_root="$(git rev-parse --show-toplevel)"
cd "${repo_root}"

if ! command -v gitleaks >/dev/null 2>&1; then
    echo "gitleaks is required but not installed."
    echo "Install: https://github.com/gitleaks/gitleaks#installing"
    exit 1
fi

if [[ ! -f ".githooks/pre-commit" ]]; then
    echo "Missing required hook file: .githooks/pre-commit"
    exit 1
fi

git config core.hooksPath .githooks
chmod +x .githooks/pre-commit

configured_path="$(git config --get core.hooksPath || true)"
if [[ "${configured_path}" != ".githooks" ]]; then
    echo "Failed to configure git hooks path."
    exit 1
fi

echo "✅ Bethuya secret leak prevention enabled."
echo "   hooksPath: ${configured_path}"
