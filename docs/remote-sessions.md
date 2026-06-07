# Remote / Claude Code web sessions

Guidance for running this repo in the ephemeral cloud containers used by Claude
Code on the web (and similar remote-execution environments). **Local dev
machines and CI are unaffected** — CI installs the SDKs itself per
`e2e-happy-path.yml`.

## .NET SDK is not pre-installed

The remote-execution containers are **ephemeral and ship without the .NET SDK** —
`dotnet` is not on `PATH` in a fresh session, so build/test/`kiota`/swagger
regeneration all fail until you install it.

**Why it's missing:** the container is provisioned fresh from a clean clone with
no SDK layer and **no SessionStart hook configured to install one**, so nothing
puts `dotnet` on `PATH`. There is no evidence of a prior install in-session
(no `~/.dotnet`, empty NuGet cache) — it was simply never present, not "lost".

**Why the usual installer fails:** the environment's network policy is an
allowlist. `https://dot.net/...` (the `dotnet-install.sh` host) and the SDK CDNs
(`builds.dotnet.microsoft.com`, `*.azureedge.net`, `aka.ms`) are **blocked**
(the proxy returns a 21-byte `Host not in allowlist` body). What *is* allowlisted:
`packages.microsoft.com`, `api.nuget.org`, the Ubuntu mirrors
(`archive.ubuntu.com`, `security.ubuntu.com`), and `download.docker.com`.

**Workaround — install via apt from `packages.microsoft.com`** (root, Ubuntu 24.04):
```bash
# 1. Some preinstalled PPAs (deadsnakes, ondrej/php @ ppa.launchpadcontent.net)
#    are NOT allowlisted and break `apt-get update` — disable them first:
for f in /etc/apt/sources.list.d/*.sources; do
  grep -q launchpadcontent "$f" && mv "$f" "$f.disabled"; done
# 2. Add the Microsoft prod repo + refresh:
curl -sSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -o /tmp/ms.deb
dpkg -i /tmp/ms.deb && apt-get update
# 3. Install both SDKs (10.0 to build/run; 8.0 for the Kiota tooling):
DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-10.0 dotnet-sdk-8.0
```
This yields `dotnet 10.0.1xx` (satisfies `global.json`'s `10.0.0` +
`rollForward: feature`) and `8.0.1xx`, installed to `/usr/lib/dotnet` with a
`/usr/bin/dotnet` symlink (persists across Bash calls within the session, but is
**lost when the container is reclaimed**). `dotnet restore`/`tool restore` then
work against the allowlisted `api.nuget.org`.

> To make this automatic for every web session, add a **SessionStart hook** that
> runs the steps above (see the `session-start-hook` skill). Until then, run them
> manually at the start of any web session that needs to build, test, or
> regenerate the Kiota client / `swagger.json`.

## Chromium for Playwright (storyboard / E2E)

The Playwright browser CDN is **blocked** by the same allowlist, so
`playwright.ps1 install chromium` fails. Install Microsoft Edge from the
allowlisted `packages.microsoft.com` and point Playwright at it via the
`PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` env var (both fixtures honour it):

```bash
DEBIAN_FRONTEND=noninteractive apt-get install -y microsoft-edge-stable powershell
PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/microsoft-edge \
  dotnet test tests/Farkle.E2eTests/Farkle.E2eTests.csproj --no-build \
  --filter "Category=Storyboard"
```
