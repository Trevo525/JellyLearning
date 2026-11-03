#!/usr/bin/env python3
"""End-to-end check of the /ELearning API on the dev instance.

Runs as the test user (JELLYFIN_TEST_USER / JELLYFIN_TEST_PASSWORD, created if missing) and as
an "outsider" user without library access (same password, created if missing). Compares
outlines with the course index, checks file streaming, rendering and its security headers,
document lecture completion, and that unknown IDs and other users' libraries are refused.
Usage: set -a; . dev/.env; set +a; dev/test-api.py"""
import json, os, pathlib, sys, urllib.error, urllib.parse, urllib.request

BASE = os.environ.get("JELLYFIN_URL", "http://localhost:8096")
KEY, USER, PASSWORD = os.environ["JELLYFIN_API_KEY"], os.environ["JELLYFIN_TEST_USER"], os.environ["JELLYFIN_TEST_PASSWORD"]
OUTSIDER = USER + "-outsider"
DEV = pathlib.Path(__file__).resolve().parent
INDEX = json.loads((DEV / "jellyfin/config/data/data/elearning/course-index.json").read_text())
HOST_ROOTS = {"/data/courses": DEV / "jellyfin/courses",
              "/data/elearning": pathlib.Path(os.environ.get("ELEARNING_SOURCE", "/path/to/courses"))}

def request(method, path, token=KEY, headers=None, body=None, auth=True, **query):
    url = f"{BASE}{path}" + (f"?{urllib.parse.urlencode(query)}" if query else "")
    h = {"Content-Type": "application/json", **(headers or {})}
    if auth:
        h["Authorization"] = f'MediaBrowser Client="api-test", Device="script", DeviceId="api-test", Version="1.0", Token="{token}"'
    req = urllib.request.Request(url, method=method, headers=h, data=None if body is None else json.dumps(body).encode())
    try:
        with urllib.request.urlopen(req) as r:
            return r.status, r.headers, r.read()
    except urllib.error.HTTPError as e:
        return e.code, e.headers, e.read()

def call(method, path, token=KEY, body=None, **query):
    status, _, data = request(method, path, token, body=body, **query)
    assert status < 300, f"{method} {path} -> {status} {data[:200]!r}"
    return json.loads(data) if data else None

def login(name):
    if not any(u["Name"] == name for u in call("GET", "/Users")):
        call("POST", "/Users/New", body={"Name": name, "Password": PASSWORD})
    return call("POST", "/Users/AuthenticateByName", token="", body={"Username": name, "Pw": PASSWORD})

token = login(USER)["AccessToken"]
outsider = login(OUTSIDER)
policy = outsider["User"]["Policy"]
if policy["EnableAllFolders"] or policy["EnabledFolders"]:
    call("POST", f"/Users/{outsider['User']['Id']}/Policy", body={**policy, "EnableAllFolders": False, "EnabledFolders": []})
outsider_token = outsider["AccessToken"]

series = call("GET", "/Items", token=token, recursive="true", includeItemTypes="Series", fields="Path")["Items"]
series_by_path = {s["Path"]: s for s in series}

def host_path(course_path, relative):
    for root, host in HOST_ROOTS.items():
        if course_path.startswith(root + "/"):
            return host / course_path[len(root) + 1:] / relative
    raise KeyError(course_path)

def walk(course):
    for s in course["Sections"]:
        for l in s["Lectures"]:
            yield s, l

results = []
def check(name, ok, detail=""):
    results.append(ok)
    print(f"{'PASS' if ok else 'FAIL'}  {name}" + ("" if ok or not detail else f"  ({detail})"))

# 1. Every indexed course with a series has an outline matching the index.
mismatches, outlines = [], {}
for entry in INDEX:
    s = series_by_path.get(entry["Path"])
    if s is None:
        continue
    outline = call("GET", f"/ELearning/Courses/{s['Id']}/Outline", token=token)
    outlines[entry["Path"]] = outline
    course = entry["Course"]
    expected = [(l["Id"], l["Title"]) for _, l in walk(course)]
    got = [(l["Id"], l["Title"]) for _, l in walk(outline)]
    videos = [l for _, l in walk(outline) if l["Kind"] == "Video"]
    if got != expected or outline["Progress"]["Total"] != len(expected) or any(not v.get("ItemId") for v in videos):
        mismatches.append(entry["Course"]["FolderName"])
check(f"outline matches index for {len(outlines)} courses ({sum(o['Progress']['Total'] for o in outlines.values())} lectures, all videos have item IDs)",
      len(outlines) >= 8 and not mismatches, ", ".join(mismatches))

ids = [x["Id"] for ol in outlines.values() for _, l in walk(ol) for x in [l, *l["Attachments"]]] + [f["Id"] for ol in outlines.values() for f in ol["Resources"]]
check(f"file IDs unique across libraries, even for the same course in both ({len(ids)} IDs)", len(ids) == len(set(ids)))

mastery = next(p for p in outlines if "IvatETA.Mastery" in p)
linked = next(p for p in outlines if "Usinavaki" in p)
notes = next(p for p in outlines if p.endswith("Sample - Markdown Notes"))
o = outlines[mastery]
season_section = next(s for s in o["Sections"] if s.get("SeasonId"))
episode = next(l for l in season_section["Lectures"] if l["Kind"] == "Video")
via_episode = call("GET", f"/ELearning/Courses/{episode['ItemId']}/Outline", token=token)
via_season = call("GET", f"/ELearning/Courses/{season_section['SeasonId']}/Outline", token=token)
check("outline by episode / season ID equals outline by series ID", via_episode == o == via_season)
check("quiz-only sections have no season", any(not s.get("SeasonId") and all(l["Kind"] != "Video" for l in s["Lectures"]) for s in o["Sections"]))

stubs = [f for _, l in walk(outlines[linked]) for f in l["Attachments"] if f.get("ExternalUrl")]
check(f"abag stubs exposed as kalifilo links ({len(stubs)} in the Usinavaki course)", len(stubs) >= 100 and all(f.get("ExternalUrl").startswith("http") for f in stubs))

# 2. Files.
pdf = next(f for _, l in walk(o) for f in l["Attachments"] if f["Kind"] == "Pdf")
pdf_path = host_path(mastery, next(f["RelativePath"] for _, l in walk(next(e["Course"] for e in INDEX if e["Path"] == mastery)) for f in l["Attachments"] if f["Id"] == pdf["Id"]))
status, headers, body = request("GET", f"/ELearning/Files/{pdf['Id']}", token, headers={"Range": "bytes=0-99"})
check("file range request returns 206 with the exact bytes", status == 206 and body == pdf_path.read_bytes()[:100] and headers["Content-Type"] == "application/pdf", f"{status} {headers.get('Content-Type')}")
check("PDF is served without a sandbox CSP (browser PDF viewer)", "Content-Security-Policy" not in headers)
check("file size in outline matches disk", pdf.get("Size") == pdf_path.stat().st_size)
status, headers, _ = request("GET", f"/ELearning/Files/{pdf['Id']}", token, download="true")
check("download=true sends an attachment with the display name", status == 200 and headers.get("Content-Disposition", "").startswith("attachment") and pdf["Name"].split(".")[0] in headers["Content-Disposition"], headers.get("Content-Disposition"))
stub = stubs[0]
status, headers, _ = request("GET", f"/ELearning/Files/{stub['Id']}", token)
check("HTML served as a file gets the no-script sandbox CSP", status == 200 and headers.get("Content-Security-Policy", "").startswith("sandbox allow-popups") and "script-src" not in headers["Content-Security-Policy"] and headers.get("X-Content-Type-Options") == "nosniff")

# 3. Render.
quiz = next(l for _, l in walk(o) if l["Kind"] == "Html" and "Quiz" in l["Title"])
quiz_rel = next(l["RelativePath"] for _, l in walk(next(e["Course"] for e in INDEX if e["Path"] == mastery)) if l["Id"] == quiz["Id"])
status, headers, body = request("GET", f"/ELearning/Files/{quiz['Id']}/Render", token)
csp = headers.get("Content-Security-Policy", "")
check("quiz renders as-is with scripts sandboxed (no same-origin, no network)",
      status == 200 and body == host_path(mastery, quiz_rel).read_bytes() and csp.startswith("sandbox allow-scripts") and "allow-same-origin" not in csp and "connect-src 'none'" in csp, f"{status} {csp}")
status, headers, body = request("GET", f"/ELearning/Files/{stub['Id']}/Render", token)
check("link stub renders as a link page", status == 200 and stub.get("ExternalUrl").split("#")[0].encode() in body and "script-src" not in headers.get("Content-Security-Policy", "x"))
status, _, _ = request("GET", f"/ELearning/Files/{quiz['Id']}/Render", token, api_key=token)
check("render refuses a token in the URL", status == 400, status)
status, _, _ = request("GET", f"/ELearning/Files/{pdf['Id']}/Render", token)
check("render refuses non-documents", status == 400, status)
n = outlines[notes]
md = next(l for _, l in walk(n) if l["Kind"] == "Markdown")
status, headers, body = request("GET", f"/ELearning/Files/{md['Id']}/Render", token)
check("markdown lecture renders to HTML", status == 200 and b"<h1" in body and b"Fixture document" in body and headers["Content-Type"].startswith("text/html"), f"{status} {body[:120]!r}")
txt = next(l for _, l in walk(n) if l["Kind"] == "Text")
status, _, body = request("GET", f"/ELearning/Files/{txt['Id']}/Render", token)
check("text lecture renders in <pre>", status == 200 and b"<pre>fixture: " in body, body[:120])

# 4. Completion of document lectures.
path = f"/ELearning/Lectures/{quiz['Id']}/Complete"
request("DELETE", path, token)
before = call("GET", f"/ELearning/Courses/{o['SeriesId']}/Outline", token=token)["Progress"]["Completed"]
status, _, _ = request("POST", path, token)
after = call("GET", f"/ELearning/Courses/{o['SeriesId']}/Outline", token=token)
done = next(l for _, l in walk(after) if l["Id"] == quiz["Id"])["Completed"]
check("POST Complete marks a quiz completed and counts it", status == 204 and done and after["Progress"]["Completed"] == before + 1, f"{status} {before}->{after['Progress']['Completed']}")
status, _, _ = request("DELETE", path, token)
again = call("GET", f"/ELearning/Courses/{o['SeriesId']}/Outline", token=token)["Progress"]["Completed"]
check("DELETE Complete undoes it", status == 204 and again == before)
status, _, _ = request("POST", f"/ELearning/Lectures/{episode['Id']}/Complete", token)
check("video lectures can't be completed through the plugin", status == 400, status)

# 5. Refusals.
for name, (method, p) in {
    "unknown file ID": ("GET", "/ELearning/Files/0123456789abcdef01234567"),
    "path-like file ID": ("GET", "/ELearning/Files/..%2F..%2Fetc%2Fpasswd"),
    "unknown lecture ID": ("POST", "/ELearning/Lectures/0123456789abcdef01234567/Complete"),
    "library folder instead of a course": ("GET", f"/ELearning/Courses/{call('GET', '/Library/VirtualFolders')[0]['ItemId']}/Outline"),
}.items():
    status, _, _ = request(method, p, token)
    check(f"{name} -> 404", status == 404, status)
status, _, _ = request("GET", f"/ELearning/Courses/{o['SeriesId']}/Outline", auth=False)
check("no token -> 401", status == 401, status)
for name, p in {"outline": f"/ELearning/Courses/{o['SeriesId']}/Outline", "file": f"/ELearning/Files/{pdf['Id']}", "render": f"/ELearning/Files/{quiz['Id']}/Render"}.items():
    status, _, _ = request("GET", p, outsider_token)
    check(f"user without library access: {name} -> 404", status == 404, status)
status, _, _ = request("POST", path, outsider_token)
check("user without library access: complete -> 404", status == 404, status)

# 6. Web UI injection (File Transformation).
status, _, page = request("GET", "/web/index.html", auth=False)
import re
tag = re.search(rb'src="\.\./ELearning/Web/elearning\.js\?v=([0-9a-f]+)"', page)
check("index.html loads the course UI script", status == 200 and tag is not None)
if tag:
    status, headers, script = request("GET", f"/ELearning/Web/elearning.js?v={tag.group(1).decode()}", auth=False)
    check("script served without login, cacheable", status == 200 and b"viewshow" in script and "immutable" in headers.get("Cache-Control", ""))

status, headers, page = request("GET", "/ELearning/Web/player.html", auth=False)
check("course player page served (no-cache, versioned files)", status == 200 and headers.get("Cache-Control") == "no-cache" and b"player.js?v=" in page and b"{{version}}" not in page)

# 7. Streaming as the course player requests it (same device profile as player.js).
PROFILE = {"MaxStreamingBitrate": 120000000, "MaxStaticBitrate": 100000000,
           "DirectPlayProfiles": [{"Container": "mp4,m4v", "Type": "Video", "VideoCodec": "h264", "AudioCodec": "aac,mp3,opus,flac"}],
           "TranscodingProfiles": [{"Container": "ts", "Type": "Video", "VideoCodec": "h264", "AudioCodec": "aac,mp3", "Context": "Streaming",
                                    "Protocol": "hls", "MaxAudioChannels": "2", "MinSegments": 1, "BreakOnNonKeyFrames": True}],
           "ContainerProfiles": [], "CodecProfiles": [], "SubtitleProfiles": [{"Format": "vtt", "Method": "External"}]}
user_id = call("GET", "/Users/Me", token=token)["Id"]
def playback_info(item_id):
    return call("POST", f"/Items/{item_id}/PlaybackInfo", token=token, body={"UserId": user_id, "DeviceProfile": PROFILE, "AutoOpenLiveStream": False,
                "EnableDirectPlay": True, "EnableDirectStream": True, "EnableTranscoding": True, "AllowVideoStreamCopy": True, "AllowAudioStreamCopy": True})
videos = [l for ol in outlines.values() for _, l in walk(ol) if l["Kind"] == "Video" and l.get("ItemId")]
paths = {l["Id"]: l["RelativePath"] for e in INDEX for _, l in walk(e["Course"])}
mp4 = next(l for l in videos if paths[l["Id"]].endswith(".mp4"))
mkv = next(l for l in videos if paths[l["Id"]].endswith(".mkv"))
info = playback_info(mp4["ItemId"])
check("mp4 lecture: direct play", info["MediaSources"][0]["SupportsDirectPlay"])
info = playback_info(mkv["ItemId"])
src = info["MediaSources"][0]
ok = False
if src.get("TranscodingUrl"):
    status, _, master = request("GET", src["TranscodingUrl"], auth=False)
    variant = next(l for l in master.decode().splitlines() if l and not l.startswith("#"))
    status2, _, media = request("GET", f"/videos/{mkv['ItemId']}/{variant}", auth=False)
    segment = next(l for l in media.decode().splitlines() if l and not l.startswith("#"))
    status3, headers3, data = request("GET", f"/videos/{mkv['ItemId']}/{segment}", auth=False)
    ok = status == status2 == status3 == 200 and len(data) > 1000
    call("POST", "/Sessions/Playing/Stopped", token=token, body={"ItemId": mkv["ItemId"], "MediaSourceId": src["Id"], "PlaySessionId": info["PlaySessionId"], "PositionTicks": 0})
check("mkv lecture: HLS from Jellyfin (playlist + first segment load with the URL's key)", ok)

print(f"{sum(results)}/{len(results)} passed")
sys.exit(0 if all(results) else 1)
