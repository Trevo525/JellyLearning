#!/usr/bin/env python3
"""Print the release notes for the build workflow: the changelog from build.yaml plus install steps."""
import re

text = open("build.yaml", encoding="utf-8").read()
m = re.search(r"^changelog:\s*>\s*\n((?:[ \t]+.*\n?)+)", text, re.M)
print(" ".join(l.strip() for l in m.group(1).splitlines()) if m else "")
print()
print("Install: unzip into Jellyfin's plugins folder (`<config>/data/plugins/`) and restart Jellyfin."
      " Requires the File Transformation plugin for the web UI.")
