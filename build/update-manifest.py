#!/usr/bin/env python3
"""Add a release to the plugin repository manifest (manifest.json) for Jellyfin's plugin catalog.
Usage: build/update-manifest.py <tag> <zip> <manifest.json in> [<manifest.json out>]
Reads the plugin's metadata from build.yaml; the checksum is the zip's MD5, which Jellyfin requires."""
import hashlib, json, os, re, sys
from datetime import datetime, timezone

REPO = "Trevo525/JellyLearning"
tag, zip_path, manifest_in = sys.argv[1:4]
manifest_out = sys.argv[4] if len(sys.argv) > 4 else manifest_in

text = open("build.yaml", encoding="utf-8").read()


def scalar(key):
    m = re.search(rf'^{key}:\s*"?(.*?)"?\s*$', text, re.M)
    return m.group(1) if m else ""


def folded(key):
    m = re.search(rf'^{key}:\s*>\s*\n((?:[ \t]+.*\n?)+)', text, re.M)
    return " ".join(line.strip() for line in m.group(1).splitlines()).strip() if m else scalar(key)


version = tag.lstrip("v")
while version.count(".") < 3:
    version += ".0"
with open(zip_path, "rb") as f:
    checksum = hashlib.md5(f.read()).hexdigest().upper()

entry = {
    "version": version,
    "changelog": folded("changelog"),
    "targetAbi": scalar("targetAbi"),
    "sourceUrl": f"https://github.com/{REPO}/releases/download/{tag}/{os.path.basename(zip_path)}",
    "checksum": checksum,
    "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S"),
}

manifest = json.load(open(manifest_in, encoding="utf-8"))
plugin = next(p for p in manifest if p["guid"] == scalar("guid"))
plugin["versions"] = [entry] + [v for v in plugin["versions"] if v["version"] != version]
with open(manifest_out, "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2)
    f.write("\n")
print(f"{plugin['name']} {version} -> {manifest_out}")
