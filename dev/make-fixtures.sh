#!/usr/bin/env bash
# Rebuild every fixture layout (tests/.../Fixtures/Listings/*.txt) as a tiny real course under
# dev/jellyfin/courses/ (= /data/courses in the dev instance). Videos are one 30 s ffmpeg test
# clip, hard-linked; documents get placeholder content. Safe to re-run (recreates the folder).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
listings="$root/tests/Jellyfin.Plugin.ELearning.Tests/Fixtures/Listings"
out="$root/dev/jellyfin/courses"
work="$root/dev/jellyfin/.fixture-clips"; rm -rf "$work"; mkdir -p "$work"; trap 'rm -rf "$work"' EXIT  # same filesystem, for hard links

for ext in mp4 mkv webm; do
  ffmpeg -loglevel error -f lavfi -i testsrc=size=320x180:rate=15:duration=30 \
    -f lavfi -i sine=frequency=440:duration=30 -shortest -c:v libx264 -preset ultrafast -c:a aac \
    -y "$work/clip.$ext" 2>/dev/null \
    || ffmpeg -loglevel error -f lavfi -i testsrc=size=320x180:rate=15:duration=30 -y "$work/clip.$ext"
done
printf '1\n00:00:01,000 --> 00:00:05,000\nFixture subtitle\n' > "$work/sub.srt"
printf 'WEBVTT\n\n00:00:01.000 --> 00:00:05.000\nFixture subtitle\n' > "$work/sub.vtt"
printf '%%PDF-1.1\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>endobj\ntrailer<</Root 1 0 R>>\n%%%%EOF\n' > "$work/doc.pdf"

rm -rf "$out"; mkdir -p "$out"
for listing in "$listings"/*.txt; do
  course="$out/$(basename "$listing" .txt)"
  while IFS= read -r rel; do
    [ -n "$rel" ] || continue
    dest="$course/$rel"; mkdir -p "$(dirname "$dest")"
    case "${rel,,}" in
      *.mp4|*.m4v|*.mov|*.avi) ln "$work/clip.mp4" "$dest" ;;
      *.mkv) ln "$work/clip.mkv" "$dest" ;;
      *.webm) ln "$work/clip.webm" "$dest" ;;
      *.srt) cp "$work/sub.srt" "$dest" ;;
      *.vtt) cp "$work/sub.vtt" "$dest" ;;
      *.pdf) cp "$work/doc.pdf" "$dest" ;;
      *.html|*.htm) printf '<!DOCTYPE html><html><body><h1>%s</h1></body></html>\n' "$(basename "$rel")" > "$dest" ;;
      *.md) printf '# %s\n\nFixture document.\n' "$(basename "$rel")" > "$dest" ;;
      *) printf 'fixture: %s\n' "$rel" > "$dest" ;;
    esac
  done < "$listing"
  echo "built $(basename "$course")"
done
du -sh "$out"
