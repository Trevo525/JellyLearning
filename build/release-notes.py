#!/usr/bin/env python3
"""Print the release notes for the build workflow: the changelog from build.yaml plus install steps."""
import re

text = open("build.yaml", encoding="utf-8").read()
m = re.search(r"^changelog:\s*>\s*\n((?:[ \t]+.*\n?)+)", text, re.M)
print(" ".join(l.strip() for l in m.group(1).splitlines()) if m else "")
print()
print("Install: add the plugin repository `https://raw.githubusercontent.com/Trevo525/JellyLearning/main/manifest.json`"
      " (Dashboard → Plugins → Repositories), then install File Transformation and JellyLearning from the catalog and restart"
      " Jellyfin. Manual install: unzip into a new folder `<config>/data/plugins/JellyLearning_<version>.0/` and restart.")
