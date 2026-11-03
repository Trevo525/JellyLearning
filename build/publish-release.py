#!/usr/bin/env python3
"""Create a Forgejo release for a tag and attach the files of a folder (used by the build workflow).

Usage: TOKEN=... build/publish-release.py <tag> <folder>
Uses GITHUB_SERVER_URL and GITHUB_REPOSITORY from the Actions environment. Release notes are
the changelog from build.yaml."""
import json, os, re, sys, urllib.request, uuid

tag, folder = sys.argv[1], sys.argv[2]
api = f"{os.environ['GITHUB_SERVER_URL']}/api/v1/repos/{os.environ['GITHUB_REPOSITORY']}"
token = os.environ["TOKEN"]

def call(method, url, data=None, content_type="application/json"):
    req = urllib.request.Request(url, method=method, data=data,
                                 headers={"Authorization": f"token {token}", "Content-Type": content_type, "Accept": "application/json"})
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read() or b"null")

text = open("build.yaml", encoding="utf-8").read()
m = re.search(r"^changelog:\s*>\s*\n((?:[ \t]+.*\n?)+)", text, re.M)
notes = " ".join(l.strip() for l in m.group(1).splitlines()) if m else ""
notes += "\n\nInstall: unzip into Jellyfin's plugins folder (`<config>/data/plugins/`) and restart Jellyfin. Requires the File Transformation plugin for the web UI."

release = call("POST", f"{api}/releases", json.dumps({"tag_name": tag, "name": tag, "body": notes}).encode())
for name in sorted(os.listdir(folder)):
    path = os.path.join(folder, name)
    if not os.path.isfile(path):
        continue
    boundary = uuid.uuid4().hex
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"attachment\"; filename=\"{name}\"\r\n"
            "Content-Type: application/octet-stream\r\n\r\n").encode() + open(path, "rb").read() + f"\r\n--{boundary}--\r\n".encode()
    call("POST", f"{api}/releases/{release['id']}/assets?name={name}", body, f"multipart/form-data; boundary={boundary}")
    print("attached", name)
print(release.get("html_url", ""))
