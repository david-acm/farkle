#!/bin/bash
# SessionStart hook — prepares a Claude Code on the web container to build/test HotDice.
#
# Fresh remote containers ship without the .NET SDK and with PostgreSQL stopped, so
# build/test/Kiota and the Marten (#302) integration work all fail until both are set up.
# This runs those one-time steps automatically at session start. The rationale behind
# each step (network allowlist, apt-from-packages.microsoft.com) is in docs/remote-sessions.md.
#
# Synchronous, idempotent, non-interactive. No-op outside the remote environment.
set -euo pipefail

# Remote-only: local dev machines and CI already have the SDK and their own Postgres.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

log() { echo "[session-start] $*"; }

# ── 1. .NET SDK (10.0 to build/run, 8.0 for the Kiota tooling) ──────────────────────
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  log ".NET SDK 10 already present ($(dotnet --version)) — skipping install"
else
  log "Installing .NET SDK 10.0 + 8.0 from packages.microsoft.com"

  # Some preinstalled PPAs (deadsnakes, ondrej/php @ ppa.launchpadcontent.net) are NOT
  # in the network allowlist and break `apt-get update` — disable them first.
  for f in /etc/apt/sources.list.d/*.sources; do
    [ -e "$f" ] || continue
    if grep -q launchpadcontent "$f"; then mv "$f" "$f.disabled"; fi
  done

  # Add the Microsoft prod repo (idempotent — dpkg re-installs cleanly) and refresh.
  curl -sSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -o /tmp/ms.deb
  dpkg -i /tmp/ms.deb
  apt-get update
  DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-10.0 dotnet-sdk-8.0
  log ".NET SDK installed ($(dotnet --version))"
fi

# ── 2. PostgreSQL 16 ────────────────────────────────────────────────────────────────
# Marten is the #302 event store; the WebTests spin their own Testcontainers, so this
# host cluster serves the Marten spike/handlers and local runs. Role postgres/changeit,
# database farkle_marten (ADR 0003).
if pg_lsclusters 2>/dev/null | grep -qE '^16\s+main\s+\S+\s+online'; then
  log "PostgreSQL 16 already running"
else
  log "Starting PostgreSQL 16 cluster"
  pg_ctlcluster 16 main start || true
fi

# Ensure the superuser password and the Marten database exist (both idempotent).
sudo -u postgres psql -tAc "ALTER USER postgres PASSWORD 'changeit';" >/dev/null
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='farkle_marten'" | grep -q 1; then
  sudo -u postgres createdb farkle_marten
  log "Created database farkle_marten"
fi
log "PostgreSQL ready on localhost:5432 (postgres/changeit · db farkle_marten)"
