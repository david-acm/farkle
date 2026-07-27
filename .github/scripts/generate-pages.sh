#!/usr/bin/env bash
#
# generate-pages.sh — build the GitHub Pages site for E2E artifacts.
#
# Runs in one of two MODEs, each publishing into the same runs/{run_id}/ tree and the
# same root table (different column / different per-run page), so the two parallel CI
# publishers (e2e videos + storyboard screenshots) don't clobber each other:
#
#   MODE=videos       (default) copies Playwright *.webm recordings into runs/{id}/ and
#                     writes runs/{id}/index.html      (embedded <video> tags)
#   MODE=screenshots            copies *.png frames into runs/{id}/screenshots/ and
#                     writes runs/{id}/storyboard.html (frames grouped by viewport)
#
# Both modes:
#   - merge metadata.json (preserving the other mode's artifact list + the videos run's
#     status/scalars), so publishing order doesn't matter
#   - regenerate the root index.html (table of all recent runs, Videos + Storyboard cols)
#   - prune runs older than 90 days or beyond the 50-run limit
#   - ensure .nojekyll exists
#
# All inputs are passed via environment variables (see tests/scripts/generate-pages.test.sh):
#
#   PAGES_DIR       (required) checkout of the gh-pages branch to write into
#   MODE            (optional) "videos" (default) | "screenshots"
#   VIDEOS_DIR      (optional) directory containing the downloaded *.webm files (videos mode)
#   SCREENSHOTS_DIR (optional) directory containing the downloaded *.png files (screenshots mode)
#   RUN_ID          (required) GitHub Actions run id
#   PR_NUMBER       (optional) pull-request number
#   BRANCH          (optional) head branch name
#   COMMIT_SHA      (optional) commit sha that triggered the run
#   STATUS          (optional) "success" | "failure" (default: "unknown")
#   REPO            (optional) owner/repo, used for source links
#   TIMESTAMP       (optional) ISO-8601 UTC; defaults to now
#   MAX_RUNS        (optional) run-count cap (default: 50)
#   MAX_AGE_DAYS    (optional) age cap in days (default: 90)
#
set -euo pipefail

PAGES_DIR="${PAGES_DIR:?PAGES_DIR is required}"
MODE="${MODE:-videos}"
VIDEOS_DIR="${VIDEOS_DIR:-}"
SCREENSHOTS_DIR="${SCREENSHOTS_DIR:-}"
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
META="$RUN_DIR/metadata.json"
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

short_sha() { printf '%s' "${1:0:7}"; }

# --- JSON helpers -------------------------------------------------------------
# Build the inner body of a JSON array from the given items: a b c -> "a", "b", "c"
json_array() {
  local out="" i
  for i in "$@"; do
    [ -n "$out" ] && out+=", "
    out+="\"$i\""
  done
  printf '%s' "$out"
}

# Read the inner body of a flat JSON string-array field from a metadata file.
extract_array() { # <key> <metafile>
  local key="$1" meta="$2"
  [ -f "$meta" ] || return 0
  grep -o "\"$key\"[[:space:]]*:[[:space:]]*\[[^]]*\]" "$meta" \
    | head -1 | sed 's/.*\[//; s/\].*//'
}

# Read a scalar JSON string field from a metadata file.
extract_scalar() { # <key> <metafile>
  local key="$1" meta="$2"
  [ -f "$meta" ] || return 0
  grep -o "\"$key\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" "$meta" \
    | head -1 | sed 's/.*:[[:space:]]*"//; s/"$//'
}

# --- 1. Collect artifacts for this mode; preserve the other mode's list -------
if [ "$MODE" = "videos" ]; then
  VIDEOS=()
  if [ -n "$VIDEOS_DIR" ] && [ -d "$VIDEOS_DIR" ]; then
    while IFS= read -r -d '' webm; do
      cp "$webm" "$RUN_DIR/"
      VIDEOS+=("$(basename "$webm")")
    done < <(find "$VIDEOS_DIR" -type f -name '*.webm' -print0 | sort -z)
  fi
  VIDEOS_JSON="$( [ "${#VIDEOS[@]}" -gt 0 ] && json_array "${VIDEOS[@]}" || true )"
  SHOTS_JSON="$(extract_array screenshots "$META")"
  # The videos run (the e2e result) owns the status + scalar metadata.
  M_STATUS="$STATUS"; M_PR="$PR_NUMBER"; M_BRANCH="$BRANCH"; M_COMMIT="$COMMIT_SHA"; M_TS="$TIMESTAMP"
else
  SHOTS=()
  if [ -n "$SCREENSHOTS_DIR" ] && [ -d "$SCREENSHOTS_DIR" ]; then
    mkdir -p "$RUN_DIR/screenshots"
    while IFS= read -r -d '' png; do
      cp "$png" "$RUN_DIR/screenshots/"
      SHOTS+=("$(basename "$png")")
    done < <(find "$SCREENSHOTS_DIR" -type f -name '*.png' -print0 | sort -z)
  fi
  SHOTS_JSON="$( [ "${#SHOTS[@]}" -gt 0 ] && json_array "${SHOTS[@]}" || true )"
  VIDEOS_JSON="$(extract_array videos "$META")"
  # Don't overwrite the videos run's status/scalars if it already published this run.
  if [ -f "$META" ]; then
    M_STATUS="$(extract_scalar status "$META")"; M_PR="$(extract_scalar pr_number "$META")"
    M_BRANCH="$(extract_scalar branch "$META")"; M_COMMIT="$(extract_scalar commit "$META")"
    M_TS="$(extract_scalar timestamp "$META")"
  else
    M_STATUS="$STATUS"; M_PR="$PR_NUMBER"; M_BRANCH="$BRANCH"; M_COMMIT="$COMMIT_SHA"; M_TS="$TIMESTAMP"
  fi
fi

# --- 2. metadata.json (merged) ------------------------------------------------
{
  printf '{\n'
  printf '  "run_id": "%s",\n'      "$RUN_ID"
  printf '  "pr_number": "%s",\n'   "$M_PR"
  printf '  "branch": "%s",\n'      "$M_BRANCH"
  printf '  "commit": "%s",\n'      "$M_COMMIT"
  printf '  "timestamp": "%s",\n'   "$M_TS"
  printf '  "status": "%s",\n'      "$M_STATUS"
  printf '  "videos": [%s],\n'      "$VIDEOS_JSON"
  printf '  "screenshots": [%s]\n'  "$SHOTS_JSON"
  printf '}\n'
} > "$META"

# --- 3. per-run page ----------------------------------------------------------
status_badge() {
  case "$1" in
    success) printf '✅ passed' ;;
    failure) printf '❌ failed' ;;
    *)       printf '❔ %s' "$(esc "$1")" ;;
  esac
}

# Shared <dl class="meta"> block describing the run (PR / branch / commit / time).
print_meta_block() {
  printf '<dl class="meta">\n'
  [ -n "$M_PR" ]     && printf '  <dt>PR</dt><dd>#%s</dd>\n' "$(esc "$M_PR")"
  [ -n "$M_BRANCH" ] && printf '  <dt>Branch</dt><dd>%s</dd>\n' "$(esc "$M_BRANCH")"
  if [ -n "$M_COMMIT" ]; then
    if [ -n "$REPO" ]; then
      printf '  <dt>Commit</dt><dd><a href="https://github.com/%s/commit/%s"><code>%s</code></a></dd>\n' \
        "$(esc "$REPO")" "$(esc "$M_COMMIT")" "$(esc "$(short_sha "$M_COMMIT")")"
    else
      printf '  <dt>Commit</dt><dd><code>%s</code></dd>\n' "$(esc "$(short_sha "$M_COMMIT")")"
    fi
  fi
  printf '  <dt>Time</dt><dd>%s</dd>\n' "$(esc "$M_TS")"
  if [ -n "$REPO" ]; then
    printf '  <dt>Workflow run</dt><dd><a href="https://github.com/%s/actions/runs/%s">View on Actions</a></dd>\n' \
      "$(esc "$REPO")" "$(esc "$RUN_ID")"
  fi
  printf '</dl>\n'
}

PAGE_STYLE='  :root { color-scheme: light dark; }
  body { font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
         margin: 0 auto; max-width: 900px; padding: 1.5rem; line-height: 1.5; }
  a { color: #2563eb; }
  .meta { color: #6b7280; font-size: .9rem;
          display: grid; grid-template-columns: max-content 1fr;
          gap: .25rem 1rem; margin: 1rem 0; }
  .meta dt { font-weight: 600; }
  video { width: 100%; background: #000; border-radius: 8px; margin: .5rem 0 1.5rem; }
  img.frame { width: 100%; border: 1px solid #e5e7eb; border-radius: 8px; margin: .5rem 0 1.5rem; }
  h2 { margin-top: 2rem; }
  .empty { color: #9ca3af; font-style: italic; }'

if [ "$MODE" = "videos" ]; then
  {
    printf '<!DOCTYPE html>\n<html lang="en">\n<head>\n'
    printf '<meta charset="utf-8">\n'
    printf '<meta name="viewport" content="width=device-width, initial-scale=1">\n'
    printf '<title>E2E run %s — HotDice</title>\n' "$(esc "$RUN_ID")"
    printf '<style>\n%s\n</style>\n</head>\n<body>\n' "$PAGE_STYLE"
    printf '<p><a href="../../">← All runs</a></p>\n'
    printf '<h1>E2E run %s</h1>\n' "$(esc "$RUN_ID")"
    printf '<p><strong>%s</strong></p>\n' "$(status_badge "$M_STATUS")"
    print_meta_block
    # Cross-link to the storyboard page when this run has screenshots.
    if [ -n "$(printf '%s' "$SHOTS_JSON" | tr -d ' ')" ]; then
      printf '<p>📸 <a href="storyboard.html">View storyboard screenshots</a></p>\n'
    fi
    printf '<h2>Recordings</h2>\n'
    if [ -z "$(printf '%s' "$VIDEOS_JSON" | tr -d ' ')" ]; then
      printf '<p class="empty">No videos were recorded for this run.</p>\n'
    else
      while IFS= read -r v; do
        [ -n "$v" ] || continue
        printf '<h3>%s</h3>\n' "$(esc "$v")"
        printf '<video controls preload="metadata" src="%s"></video>\n' "$(esc "$v")"
      done < <(printf '%s' "$VIDEOS_JSON" | grep -o '"[^"]*"' | sed 's/^"//; s/"$//')
    fi
    printf '</body>\n</html>\n'
  } > "$RUN_DIR/index.html"
else
  {
    printf '<!DOCTYPE html>\n<html lang="en">\n<head>\n'
    printf '<meta charset="utf-8">\n'
    printf '<meta name="viewport" content="width=device-width, initial-scale=1">\n'
    printf '<title>Storyboard run %s — HotDice</title>\n' "$(esc "$RUN_ID")"
    printf '<style>\n%s\n</style>\n</head>\n<body>\n' "$PAGE_STYLE"
    printf '<p><a href="../../">← All runs</a></p>\n'
    printf '<h1>Storyboard run %s</h1>\n' "$(esc "$RUN_ID")"
    printf '<p><strong>%s</strong></p>\n' "$(status_badge "$M_STATUS")"
    print_meta_block
    # Cross-link to the recordings page when this run also has videos.
    if [ -n "$(printf '%s' "$VIDEOS_JSON" | tr -d ' ')" ]; then
      printf '<p>🎬 <a href="index.html">View happy-path recordings</a></p>\n'
    fi
    if [ -z "$(printf '%s' "$SHOTS_JSON" | tr -d ' ')" ]; then
      printf '<p class="empty">No screenshots were captured for this run.</p>\n'
    else
      mapfile -t FRAMES < <(printf '%s' "$SHOTS_JSON" | grep -o '"[^"]*"' | sed 's/^"//; s/"$//' | sort)
      # Group frames by viewport (the trailing -{viewport} suffix), each in step order.
      for vp in mobile medium large; do
        section=""
        for f in "${FRAMES[@]}"; do
          fvp="${f##*-}"; fvp="${fvp%.png}"
          [ "$fvp" = "$vp" ] || continue
          section+="<img class=\"frame\" loading=\"lazy\" src=\"screenshots/$(esc "$f")\" alt=\"$(esc "$f")\">"$'\n'
        done
        [ -n "$section" ] || continue
        printf '<h2>%s</h2>\n' "$(esc "$vp")"
        printf '%s' "$section"
      done
    fi
    printf '</body>\n</html>\n'
  } > "$RUN_DIR/storyboard.html"
fi

# --- 4. Prune -----------------------------------------------------------------
NOW_EPOCH="$(date -u +%s)"
AGE_LIMIT=$(( MAX_AGE_DAYS * 86400 ))

# 4a. Age-based prune.
for meta in "$PAGES_DIR"/runs/*/metadata.json; do
  [ -e "$meta" ] || continue
  ts="$(extract_scalar timestamp "$meta")"
  [ -n "$ts" ] || continue
  ts_epoch="$(date -u -d "$ts" +%s 2>/dev/null || echo 0)"
  [ "$ts_epoch" -eq 0 ] && continue
  if [ $(( NOW_EPOCH - ts_epoch )) -gt "$AGE_LIMIT" ]; then
    rm -rf "$(dirname "$meta")"
  fi
done

# 4b. Count-based prune — keep the newest MAX_RUNS by run id (numeric desc).
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

# --- 5. Root index.html -------------------------------------------------------
ROWS_TMP="$(mktemp)"
trap 'rm -f "$ROWS_TMP"' EXIT

for meta in "$PAGES_DIR"/runs/*/metadata.json; do
  [ -e "$meta" ] || continue
  r_id="$(extract_scalar run_id "$meta")"
  r_pr="$(extract_scalar pr_number "$meta")"
  r_branch="$(extract_scalar branch "$meta")"
  r_commit="$(extract_scalar commit "$meta")"
  r_ts="$(extract_scalar timestamp "$meta")"
  r_status="$(extract_scalar status "$meta")"
  r_vcount="$( { printf '%s' "$(extract_array videos "$meta")" | grep -o '\.webm' || true; } | wc -l | tr -d ' ')"
  r_scount="$( { printf '%s' "$(extract_array screenshots "$meta")" | grep -o '\.png' || true; } | wc -l | tr -d ' ')"
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

  # The run-id link points at whichever per-run page exists (videos page preferred).
  if [ -f "$PAGES_DIR/runs/$r_id/index.html" ]; then
    id_link="runs/$(esc "$r_id")/"
  else
    id_link="runs/$(esc "$r_id")/storyboard.html"
  fi

  if [ "$r_vcount" -gt 0 ]; then
    videos_cell="<a href=\"runs/$(esc "$r_id")/\">${r_vcount} 🎬</a>"
  else
    videos_cell="—"
  fi
  if [ "$r_scount" -gt 0 ]; then
    story_cell="<a href=\"runs/$(esc "$r_id")/storyboard.html\">${r_scount} 📸</a>"
  else
    story_cell="—"
  fi

  row="<tr>"
  row+="<td><a href=\"${id_link}\">$(esc "$r_id")</a></td>"
  row+="<td>$(status_badge "$r_status")</td>"
  row+="<td>${pr_cell}</td>"
  row+="<td>$(esc "${r_branch:-—}")</td>"
  row+="<td>${commit_cell}</td>"
  row+="<td>$(esc "${r_ts:-—}")</td>"
  row+="<td>${videos_cell}</td>"
  row+="<td>${story_cell}</td>"
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
<title>HotDice — E2E test reports</title>
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
<h1>HotDice — E2E test reports</h1>
<p class="sub">Playwright happy-path recordings (🎬) and storyboard screenshots (📸), newest first.</p>
HTML

  if [ -s "$ROWS_TMP" ]; then
    printf '<table>\n<thead><tr>'
    printf '<th>Run</th><th>Status</th><th>PR</th><th>Branch</th><th>Commit</th><th>Time (UTC)</th><th>Videos</th><th>Storyboard</th>'
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

echo "Generated pages for run $RUN_ID (mode=$MODE)."
