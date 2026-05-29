#!/usr/bin/env bash
#
# generate-pages.sh — build the GitHub Pages site for E2E test videos.
#
# Given a checkout of the `gh-pages` branch and a directory of freshly
# downloaded Playwright `.webm` recordings, this script:
#   1. Copies the videos into runs/{run_id}/
#   2. Writes runs/{run_id}/metadata.json
#   3. Generates runs/{run_id}/index.html  (embedded <video> tags)
#   4. Regenerates the root index.html      (table of all recent runs)
#   5. Prunes runs older than 90 days or beyond the 50-run limit
#   6. Ensures .nojekyll exists
#
# All inputs are passed via environment variables so the script is easy to
# exercise locally (see tests/scripts/generate-pages.test.sh):
#
#   PAGES_DIR    (required) checkout of the gh-pages branch to write into
#   VIDEOS_DIR   (optional) directory containing the downloaded *.webm files
#   RUN_ID       (required) GitHub Actions run id
#   PR_NUMBER    (optional) pull-request number
#   BRANCH       (optional) head branch name
#   COMMIT_SHA   (optional) commit sha that triggered the run
#   STATUS       (optional) "success" | "failure" (default: "unknown")
#   REPO         (optional) owner/repo, used for source links
#   TIMESTAMP    (optional) ISO-8601 UTC; defaults to now
#   MAX_RUNS     (optional) run-count cap (default: 50)
#   MAX_AGE_DAYS (optional) age cap in days (default: 90)
#
set -euo pipefail

PAGES_DIR="${PAGES_DIR:?PAGES_DIR is required}"
VIDEOS_DIR="${VIDEOS_DIR:-}"
RUN_ID="${RUN_ID:?RUN_ID is required}"
PR_NUMBER="${PR_NUMBER:-}"
BRANCH="${BRANCH:-}"
COMMIT_SHA="${COMMIT_SHA:-}"
STATUS="${STATUS:-unknown}"
REPO="${REPO:-}"
TIMESTAMP="${TIMESTAMP:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"
MAX_RUNS="${MAX_RUNS:-50}"
MAX_AGE_DAYS="${MAX_AGE_DAYS:-90}"

RUN_DIR="$PAGES_DIR/runs/$RUN_ID"
mkdir -p "$RUN_DIR"

# --- HTML escaping helper -----------------------------------------------------
html_escape() {
  # Escape the five XML/HTML metacharacters from stdin.
  sed -e 's/&/\&amp;/g' \
      -e 's/</\&lt;/g' \
      -e 's/>/\&gt;/g' \
      -e 's/"/\&quot;/g' \
      -e "s/'/\&#39;/g"
}

esc() { printf '%s' "$1" | html_escape; }

# --- 1. Copy videos -----------------------------------------------------------
VIDEOS=()
if [ -n "$VIDEOS_DIR" ] && [ -d "$VIDEOS_DIR" ]; then
  while IFS= read -r -d '' webm; do
    cp "$webm" "$RUN_DIR/"
    VIDEOS+=("$(basename "$webm")")
  done < <(find "$VIDEOS_DIR" -type f -name '*.webm' -print0 | sort -z)
fi

# --- 2. metadata.json ---------------------------------------------------------
{
  printf '{\n'
  printf '  "run_id": "%s",\n'    "$RUN_ID"
  printf '  "pr_number": "%s",\n' "$PR_NUMBER"
  printf '  "branch": "%s",\n'    "$BRANCH"
  printf '  "commit": "%s",\n'    "$COMMIT_SHA"
  printf '  "timestamp": "%s",\n' "$TIMESTAMP"
  printf '  "status": "%s",\n'    "$STATUS"
  printf '  "videos": ['
  for i in "${!VIDEOS[@]}"; do
    [ "$i" -gt 0 ] && printf ', '
    printf '"%s"' "${VIDEOS[$i]}"
  done
  printf ']\n'
  printf '}\n'
} > "$RUN_DIR/metadata.json"

# --- 3. per-run index.html ----------------------------------------------------
status_badge() {
  case "$1" in
    success) printf '✅ passed' ;;
    failure) printf '❌ failed' ;;
    *)       printf '❔ %s' "$(esc "$1")" ;;
  esac
}

short_sha() { printf '%s' "${1:0:7}"; }

{
  cat <<HTML
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>E2E run $(esc "$RUN_ID") — Farkle</title>
<style>
  :root { color-scheme: light dark; }
  body { font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
         margin: 0 auto; max-width: 900px; padding: 1.5rem; line-height: 1.5; }
  a { color: #2563eb; }
  .meta { color: #6b7280; font-size: .9rem;
          display: grid; grid-template-columns: max-content 1fr;
          gap: .25rem 1rem; margin: 1rem 0; }
  .meta dt { font-weight: 600; }
  video { width: 100%; background: #000; border-radius: 8px; margin: .5rem 0 1.5rem; }
  h2 { margin-top: 2rem; }
  .empty { color: #9ca3af; font-style: italic; }
</style>
</head>
<body>
<p><a href="../../">← All runs</a></p>
<h1>E2E run $(esc "$RUN_ID")</h1>
<p><strong>$(status_badge "$STATUS")</strong></p>
<dl class="meta">
HTML

  [ -n "$PR_NUMBER" ] && printf '  <dt>PR</dt><dd>#%s</dd>\n' "$(esc "$PR_NUMBER")"
  [ -n "$BRANCH" ]    && printf '  <dt>Branch</dt><dd>%s</dd>\n' "$(esc "$BRANCH")"
  if [ -n "$COMMIT_SHA" ]; then
    if [ -n "$REPO" ]; then
      printf '  <dt>Commit</dt><dd><a href="https://github.com/%s/commit/%s"><code>%s</code></a></dd>\n' \
        "$(esc "$REPO")" "$(esc "$COMMIT_SHA")" "$(esc "$(short_sha "$COMMIT_SHA")")"
    else
      printf '  <dt>Commit</dt><dd><code>%s</code></dd>\n' "$(esc "$(short_sha "$COMMIT_SHA")")"
    fi
  fi
  printf '  <dt>Time</dt><dd>%s</dd>\n' "$(esc "$TIMESTAMP")"
  if [ -n "$REPO" ]; then
    printf '  <dt>Workflow run</dt><dd><a href="https://github.com/%s/actions/runs/%s">View on Actions</a></dd>\n' \
      "$(esc "$REPO")" "$(esc "$RUN_ID")"
  fi

  printf '</dl>\n<h2>Recordings</h2>\n'

  if [ "${#VIDEOS[@]}" -eq 0 ]; then
    printf '<p class="empty">No videos were recorded for this run.</p>\n'
  else
    for v in "${VIDEOS[@]}"; do
      printf '<h3>%s</h3>\n' "$(esc "$v")"
      printf '<video controls preload="metadata" src="%s"></video>\n' "$(esc "$v")"
    done
  fi

  printf '</body>\n</html>\n'
} > "$RUN_DIR/index.html"

# --- 5. Prune -----------------------------------------------------------------
NOW_EPOCH="$(date -u +%s)"
AGE_LIMIT=$(( MAX_AGE_DAYS * 86400 ))

# 5a. Age-based prune.
for meta in "$PAGES_DIR"/runs/*/metadata.json; do
  [ -e "$meta" ] || continue
  ts="$(grep -o '"timestamp"[[:space:]]*:[[:space:]]*"[^"]*"' "$meta" \
        | head -1 | sed 's/.*:[[:space:]]*"//;s/"$//')"
  [ -n "$ts" ] || continue
  ts_epoch="$(date -u -d "$ts" +%s 2>/dev/null || echo 0)"
  [ "$ts_epoch" -eq 0 ] && continue
  if [ $(( NOW_EPOCH - ts_epoch )) -gt "$AGE_LIMIT" ]; then
    rm -rf "$(dirname "$meta")"
  fi
done

# 5b. Count-based prune — keep the newest MAX_RUNS by run id (numeric desc).
mapfile -t RUN_IDS < <(
  for d in "$PAGES_DIR"/runs/*/; do
    [ -d "$d" ] || continue
    basename "$d"
  done | sort -rn
)
if [ "${#RUN_IDS[@]}" -gt "$MAX_RUNS" ]; then
  for old in "${RUN_IDS[@]:$MAX_RUNS}"; do
    rm -rf "$PAGES_DIR/runs/$old"
  done
fi

# --- 6. Root index.html -------------------------------------------------------
ROWS_TMP="$(mktemp)"
trap 'rm -f "$ROWS_TMP"' EXIT

for meta in "$PAGES_DIR"/runs/*/metadata.json; do
  [ -e "$meta" ] || continue
  get() {
    grep -o "\"$1\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" "$meta" \
      | head -1 | sed 's/.*:[[:space:]]*"//;s/"$//'
  }
  r_id="$(get run_id)"
  r_pr="$(get pr_number)"
  r_branch="$(get branch)"
  r_commit="$(get commit)"
  r_ts="$(get timestamp)"
  r_status="$(get status)"
  r_vcount="$( { grep -o '"videos"[[:space:]]*:[[:space:]]*\[[^]]*\]' "$meta" \
                 | grep -o '\.webm' || true; } | wc -l | tr -d ' ')"
  [ -n "$r_id" ] || continue

  pr_cell="—"
  if [ -n "$r_pr" ]; then
    if [ -n "$REPO" ]; then
      pr_cell="<a href=\"https://github.com/$(esc "$REPO")/pull/$(esc "$r_pr")\">#$(esc "$r_pr")</a>"
    else
      pr_cell="#$(esc "$r_pr")"
    fi
  fi
  commit_cell="—"
  if [ -n "$r_commit" ]; then
    commit_cell="<code>$(esc "$(short_sha "$r_commit")")</code>"
  fi

  row="<tr>"
  row+="<td><a href=\"runs/$(esc "$r_id")/\">$(esc "$r_id")</a></td>"
  row+="<td>$(status_badge "$r_status")</td>"
  row+="<td>${pr_cell}</td>"
  row+="<td>$(esc "${r_branch:-—}")</td>"
  row+="<td>${commit_cell}</td>"
  row+="<td>$(esc "${r_ts:-—}")</td>"
  row+="<td>${r_vcount} 🎬</td>"
  row+="<td><a href=\"runs/$(esc "$r_id")/\">Watch videos</a></td>"
  row+="</tr>"

  # Prefix with run id for sorting (newest first), tab-separated.
  printf '%s\t%s\n' "$r_id" "$row" >> "$ROWS_TMP"
done

{
  cat <<'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Farkle — E2E test videos</title>
<style>
  :root { color-scheme: light dark; }
  body { font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
         margin: 0 auto; max-width: 1100px; padding: 1.5rem; line-height: 1.5; }
  a { color: #2563eb; }
  h1 { margin-bottom: .25rem; }
  .sub { color: #6b7280; margin-top: 0; }
  table { border-collapse: collapse; width: 100%; margin-top: 1.5rem; }
  th, td { text-align: left; padding: .55rem .75rem; border-bottom: 1px solid #e5e7eb; }
  th { font-size: .8rem; text-transform: uppercase; letter-spacing: .03em; color: #6b7280; }
  tr:hover td { background: rgba(127,127,127,.08); }
  code { font-size: .9em; }
  .empty { color: #9ca3af; font-style: italic; margin-top: 2rem; }
</style>
</head>
<body>
<h1>Farkle — E2E test videos</h1>
<p class="sub">Playwright happy-path recordings, newest first.</p>
HTML

  if [ -s "$ROWS_TMP" ]; then
    printf '<table>\n<thead><tr>'
    printf '<th>Run</th><th>Status</th><th>PR</th><th>Branch</th><th>Commit</th><th>Time (UTC)</th><th>Videos</th><th></th>'
    printf '</tr></thead>\n<tbody>\n'
    sort -t"$(printf '\t')" -k1,1 -rn "$ROWS_TMP" | cut -f2-
    printf '</tbody>\n</table>\n'
  else
    printf '<p class="empty">No runs published yet.</p>\n'
  fi

  printf '</body>\n</html>\n'
} > "$PAGES_DIR/index.html"

# --- .nojekyll ----------------------------------------------------------------
touch "$PAGES_DIR/.nojekyll"

echo "Generated pages for run $RUN_ID with ${#VIDEOS[@]} video(s)."
