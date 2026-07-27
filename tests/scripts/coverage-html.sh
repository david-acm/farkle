#!/usr/bin/env bash
# Browsable per-class HTML coverage report from the Cobertura reports produced by
# `dotnet test --collect:"XPlat Code Coverage"`.
#
# Usage: coverage-html.sh <coverage-root-dir> [output-dir]
set -euo pipefail

COV_ROOT="${1:-.}"
OUT_DIR="${2:-coverage-html}"

if ! command -v reportgenerator >/dev/null 2>&1; then
  echo "coverage-html: installing dotnet-reportgenerator-globaltool…"
  dotnet tool install --global dotnet-reportgenerator-globaltool >/dev/null
  export PATH="$PATH:$HOME/.dotnet/tools"
fi

reportgenerator \
  "-reports:${COV_ROOT}/**/coverage.cobertura.xml" \
  "-targetdir:${OUT_DIR}" \
  "-reporttypes:Html;TextSummary"

echo "coverage-html: report written to ${OUT_DIR}/index.html"
[ -f "${OUT_DIR}/Summary.txt" ] && cat "${OUT_DIR}/Summary.txt"
