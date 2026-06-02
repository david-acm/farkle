#!/usr/bin/env bash
#
# generate-pages.sh — build the GitHub Pages site for E2E artifacts.
#
# The site has one runs table (root index.html). Each run gets a directory
# runs/{run_id}/ that can hold two distinct artifact kinds, published by two
# independent (parallel) workflows:
#
#   • videos      (MODE=videos, default) — Playwright happy-path .webm recordings,
#                 shown on runs/{run_id}/index.html.
#   • screenshots (MODE=screenshots)     — multi-viewport storyboard .png frames,
#                 shown on a SEPARATE page runs/{run_id}/storyboard.html.
#
# The root table lists every run with a Videos column and a Storyboard column,
# each linking to the relevant per-run page.
#
# Because the two workflows write the same runs/{run_id}/metadata.json in either
# order, each MODE MERGES: it refreshes only its own artifact list and preserves
# the other's. This makes the result order-independent (the publish steps are
# also serialised via a shared workflow concurrency group).
#
# Steps for one invocation:
#   1. Copy this mode's artifacts into runs/{run_id}/ (or runs/{run_id}/screenshots/)
#   2. Write runs/{run_id}/metadata.json   (merging the other mode's list)
#   3. Generate this mode's per-run page    (index.html or storyboard.html)
#   4. Prune runs older than 90 days or beyond the 50-run limit
#   5. Regenerate the root index.html       (table of all recent runs)
#   6. Ensure .nojekyll exists
#
# All inputs are passed via environment variables so the script is easy to
# exercise locally (see tests/scripts/generate-pages.test.sh):
#
#   PAGES_DIR       (required) checkout of the gh-pages branch to write into
#   MODE            (optional) "videos" (default) | "screenshots"
#   VIDEOS_DIR      (optional) directory containing the downloaded *.webm files
#   SCREENSHOTS_DIR (optional) directory containing the downloaded *.png frames
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

case "$MODE" in
  videos|screenshots) ;;
  *) echo "Unknown MODE '$MODE' (expected 'videos' or 'screenshots')" >&2; exit 2 ;;
esac

RUN_DIR="$PAGES_DIR/runs/$RUN_ID"
META="$RUN_DIR/metadata.json"
mkdir -p "$RUN_DIR"

# --- helpers ------------------------------------------------------------------
html_escape() {
  # Escape the five XML/HTML metacharacters from stdin.
  sed -e 's/&/\&amp;/g' \
      -e 's/</\&lt;/g' \
      -e 's/>/\&gt;/g' \
      -e 's/"/\&quot;/g' \
      -e "s/'/\&#39;/g"
}
esc() { printf '%s' "$1" | html_escape; }

status_badge() {
  case "$1" in
    success) printf '✅ passed' ;;
    failure) printf '❌ failed' ;;
    *)       printf '❔ %s' "$(esc "$1")" ;;
  esac
}
short_sha() { printf '%s' "${1:0:7}"; }

# Read the raw inner content of a JSON string-array field from a metadata file,
# e.g. extract_array screenshots meta.json -> '"a.png", "b.png"'. Returns empty
# when the file or field is absent (used to preserve the other mode's list).
extract_array() {
  [ -f "$2" ] || return 0
  grep -o "\"$1\"[[:space:]]*:[[:space:]]*\[[^]]*\]" "$2" \
    | head -1 | sed 's/.*\[//; s/\]$//'
}

# Read a scalar JSON string field from a metadata file.
extract_scalar() {
  [ -f "$2" ] || return 0
  grep -o "\"$1\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" "$2" \
    | head -1 | sed 's/.*:[[:space:]]*"//;s/"$//'
}

# Build a JSON array body from the given items, e.g. json_array a b -> '"a", "b"'.
json_array() {
  local out="" i
  for i in "$@"; do
    [ -n "$out" ] && out+=", "
    out+="\"$i\""
  done
  printf '%s' "$out"
}

# --- 1. Collect this mode's artifacts; preserve the other mode's list ---------
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
fi

# Scalars: refresh from the environment, falling back to existing metadata when a
# value isn't supplied. Status is owned by the videos run (the e2e result), so the
# screenshots run never overwrites an existing status.
m_pr="${PR_NUMBER:-$(extract_scalar pr_number "$META")}"
m_branch="${BRANCH:-$(extract_scalar branch "$META")}"
m_commit="${COMMIT_SHA:-$(extract_scalar commit "$META")}"
m_status="$STATUS"
if [ "$MODE" = "screenshots" ]; then
  existing_status="$(extract_scalar status "$META")"
  [ -n "$existing_status" ] && m_status="$existing_status"
fi

# --- 2. metadata.json ---------------------------------------------------------
{
  printf '{\n'
  printf '  "run_id": "%s",\n'    "$RUN_ID"
  printf '  "pr_number": "%s",\n' "$m_pr"
  printf '  "branch": "%s",\n'    "$m_branch"
  printf '  "commit": "%s",\n'    "$m_commit"
  printf '  "timestamp": "%s",\n' "$TIMESTAMP"
  printf '  "status": "%s",\n'    "$m_status"
  printf '  "videos": [%s],\n'      "$VIDEOS_JSON"
  printf '  "screenshots": [%s]\n'  "$SHOTS_JSON"
  printf '}\n'
} > "$META"

# --- 3. per-run page for this mode --------------------------------------------
meta_block() {
  # Shared metadata <dl> used by both per-run pages.
  printf '<dl class="meta">\n'
  [ -n "$m_pr" ]     && printf '  <dt>PR</dt><dd>#%s</dd>\n' "$(esc "$m_pr")"
  [ -n "$m_branch" ] && printf '  <dt>Branch</dt><dd>%s</dd>\n' "$(esc "$m_branch")"
  if [ -n "$m_commit" ]; then
    if [ -n "$REPO" ]; then
      printf '  <dt>Commit</dt><dd><a href="https://github.com/%s/commit/%s"><code>%s</code></a></dd>\n' \
        "$(esc "$REPO")" "$(esc "$m_commit")" "$(esc "$(short_sha "$m_commit")")"
    else
      printf '  <dt>Commit</dt><dd><code>%s</code></dd>\n' "$(esc "$(short_sha "$m_commit")")"
    fi
  fi
  printf '  <dt>Time</dt><dd>%s</dd>\n' "$(esc "$TIMESTAMP")"
  if [ -n "$REPO" ]; then
    printf '  <dt>Workflow run</dt><dd><a href="https://github.com/%s/actions/runs/%s">View on Actions</a></dd>\n' \
      "$(esc "$REPO")" "$(esc "$RUN_ID")"
  fi
  printf '</dl>\n'
}

PAGE_CSS='  :root { color-scheme: light dark; }
  body { font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
         margin: 0 auto; max-width: 900px; padding: 1.5rem; line-height: 1.5; }
  a { color: #2563eb; }
  .meta { color: #6b7280; font-size: .9rem;
          display: grid; grid-template-columns: max-content 1fr;
          gap: .25rem 1rem; margin: 1rem 0; }
  .meta dt { font-weight: 600; }
  video { width: 100%; background: #000; border-radius: 8px; margin: .5rem 0 1.5rem; }
  img.frame { width: 100%; border: 1px solid #e5e7eb; border-radius: 8px; margin: .25rem 0 1rem; }
  h2 { margin-top: 2rem; }
  .empty { color: #9ca3af; font-style: italic; }'

if [ "$MODE" = "videos" ]; then
  # Count preserved screenshots so the videos page can cross-link to the storyboard.
  shots_count="$( { printf '%s' "$SHOTS_JSON" | grep -o '\.png' || true; } | wc -l | tr -d ' ')"
  {
    cat <<HTML
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>E2E run $(esc "$RUN_ID") — Farkle</title>
<style>
$PAGE_CSS
</style>
</head>
<body>
<p><a href="../../">← All runs</a></p>
<h1>E2E run $(esc "$RUN_ID")</h1>
<p><strong>$(status_badge "$m_status")</strong></p>
HTML
    meta_block
    [ "$shots_count" -gt 0 ] && printf '<p><a href="storyboard.html">View storyboard screenshots →</a></p>\n'
    printf '<h2>Recordings</h2>\n'
    if [ -z "$VIDEOS_JSON" ]; then
      printf '<p class="empty">No videos were recorded for this run.</p>\n'
    else
      # Re-read the (just written) video list from metadata for a single source of truth.
      while IFS= read -r v; do
        [ -n "$v" ] || continue
        printf '<h3>%s</h3>\n' "$(esc "$v")"
        printf '<video controls preload="metadata" src="%s"></video>\n' "$(esc "$v")"
      done < <(printf '%s' "$VIDEOS_JSON" | grep -o '"[^"]*"' | sed 's/^"//;s/"$//')
    fi
    printf '</body>\n</html>\n'
  } > "$RUN_DIR/index.html"
else
  # Storyboard page: group frames by viewport (the trailing -{viewport} suffix),
  # showing each viewport's frames in interaction order (filenames sort by step).
  {
    cat <<HTML
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Storyboard — run $(esc "$RUN_ID") — Farkle</title>
<style>
$PAGE_CSS
</style>
</head>
<body>
<p><a href="../../">← All runs</a> · <a href="index.html">Videos for this run →</a></p>
<h1>Storyboard — run $(esc "$RUN_ID")</h1>
<p><strong>$(status_badge "$m_status")</strong></p>
HTML
    meta_block
    if [ -z "$SHOTS_JSON" ]; then
      printf '<p class="empty">No screenshots were captured for this run.</p>\n'
    else
      # Flatten the frame list, then group by the viewport suffix.
      mapfile -t FRAMES < <(printf '%s' "$SHOTS_JSON" | grep -o '"[^"]*"' | sed 's/^"//;s/"$//' | sort)
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
      # Any frames whose suffix isn't a known viewport: show them under "other".
      other=""
      for f in "${FRAMES[@]}"; do
        fvp="${f##*-}"; fvp="${fvp%.png}"
        case "$fvp" in mobile|medium|large) ;; *)
          other+="<img class=\"frame\" loading=\"lazy\" src=\"screenshots/$(esc "$f")\" alt=\"$(esc "$f")\">"$'\n' ;;
        esac
      done
      if [ -n "$other" ]; then
        printf '<h2>other</h2>\n'
        printf '%s' "$other"
      fi
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
  r_vcount="$( { extract_array videos "$meta" | grep -o '\.webm' || true; } | wc -l | tr -d ' ')"
  r_scount="$( { extract_array screenshots "$meta" | grep -o '\.png' || true; } | wc -l | tr -d ' ')"
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
  videos_cell="—"
  if [ "$r_vcount" -gt 0 ]; then
    videos_cell="<a href=\"runs/$(esc "$r_id")/\">${r_vcount} 🎬</a>"
  fi
  storyboard_cell="—"
  if [ "$r_scount" -gt 0 ]; then
    storyboard_cell="<a href=\"runs/$(esc "$r_id")/storyboard.html\">${r_scount} 📸</a>"
  fi

  row="<tr>"
  row+="<td><a href=\"runs/$(esc "$r_id")/\">$(esc "$r_id")</a></td>"
  row+="<td>$(status_badge "$r_status")</td>"
  row+="<td>${pr_cell}</td>"
  row+="<td>$(esc "${r_branch:-—}")</td>"
  row+="<td>${commit_cell}</td>"
  row+="<td>$(esc "${r_ts:-—}")</td>"
  row+="<td>${videos_cell}</td>"
  row+="<td>${storyboard_cell}</td>"
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
<title>Farkle — E2E reports</title>
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
<h1>Farkle — E2E reports</h1>
<p class="sub">Playwright happy-path recordings and storyboard screenshots, newest first.</p>
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

# --- 6. .nojekyll -------------------------------------------------------------
touch "$PAGES_DIR/.nojekyll"

if [ "$MODE" = "videos" ]; then
  echo "Generated pages for run $RUN_ID (mode=videos)."
else
  echo "Generated pages for run $RUN_ID (mode=screenshots)."
fi
