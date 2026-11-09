#!/usr/bin/env bash
# Build the release zip for a manual install:
#   build/package.sh [version]        e.g. build/package.sh 0.1.0   (default: version in build.yaml)
# Produces artifacts/release/JellyLearning_<version>.zip with the plugin files at its root (plugin dll,
# Markdig.dll, meta.json): the layout Jellyfin extracts for a plugin repository install (manifest.json).
# Manual install: unzip into a new folder <config>/data/plugins/JellyLearning_<version>.0/, restart.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

version="${1:-$(sed -n 's/^version: *"\(.*\)"/\1/p' build.yaml)}"
version="${version#v}"
short="${version%.0}"   # the zip is named with the release version (1.0.1), the folder inside with four parts
[ "$(tr -cd . <<<"$short" | wc -c)" -ge 2 ] || short="$version"
# Jellyfin plugin versions have four parts (assembly, meta.json, folder name inside the zip).
while [ "$(tr -cd . <<<"$version" | wc -c)" -lt 3 ]; do version="$version.0"; done

out="$root/artifacts/release"
name="JellyLearning_$version"
rm -rf "$out" && mkdir -p "$out/$name"

dotnet publish Jellyfin.Plugin.ELearning/Jellyfin.Plugin.ELearning.csproj -c Release -o "$out/publish" --nologo -v quiet \
  -p:Version="$version" -p:AssemblyVersion="$version" -p:FileVersion="$version"

for artifact in $(sed -n 's/^- *"\(.*\)"/\1/p' build.yaml); do
  cp "$out/publish/$artifact" "$out/$name/"
done

# meta.json is what Jellyfin shows for a plugin (name, version, description); fields from build.yaml.
python3 - "$version" "$out/$name/meta.json" <<'PY'
import json, re, sys
from datetime import datetime, timezone
text = open("build.yaml", encoding="utf-8").read()
def scalar(key):
    m = re.search(rf'^{key}:\s*"?(.*?)"?\s*$', text, re.M)
    return m.group(1) if m else ""
def folded(key):
    m = re.search(rf'^{key}:\s*>\s*\n((?:[ \t]+.*\n?)+)', text, re.M)
    return " ".join(line.strip() for line in m.group(1).splitlines()).strip() if m else scalar(key)
meta = {
    "category": scalar("category"), "changelog": folded("changelog"), "description": folded("description"),
    "guid": scalar("guid"), "name": scalar("name"), "overview": scalar("overview"), "owner": scalar("owner"),
    "targetAbi": scalar("targetAbi"), "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "version": sys.argv[1], "status": "Active", "autoUpdate": False, "imagePath": "", "assemblies": [],
}
json.dump(meta, open(sys.argv[2], "w", encoding="utf-8"), indent=2)
PY

python3 - "$out" "$name" "JellyLearning_$short" <<'PY'
import os, sys, zipfile
out, name, zipname = sys.argv[1:4]
with zipfile.ZipFile(os.path.join(out, zipname + ".zip"), "w", zipfile.ZIP_DEFLATED) as z:
    for file in sorted(os.listdir(os.path.join(out, name))):
        z.write(os.path.join(out, name, file), file)
PY
(cd "$out" && sha256sum "JellyLearning_$short.zip" > "JellyLearning_$short.zip.sha256")
rm -rf "$out/publish"
echo "$out/JellyLearning_$short.zip"
