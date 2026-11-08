#!/usr/bin/env python3
"""Check that Jellyfin's view of every course matches the plugin's parser output.

Compares the web client's listing (Shows/{id}/Seasons + Episodes) with the plugin's course
index (dev/jellyfin/config/data/data/elearning/course-index.json, rebuilt on each library scan):
course title/studio/year, season numbers + titles, every video lecture as exactly one episode
with the right S/E number and title, sidecar subtitles exposed, and course artwork present.
Usage: dev/verify-library.py      (exit code 1 on any mismatch)"""
import json, os, sys, urllib.parse, urllib.request
from pathlib import Path
import devenv  # loads dev/.env

BASE = os.environ.get("JELLYFIN_URL", "http://localhost:8096")
KEY = os.environ["JELLYFIN_API_KEY"]
INDEX = devenv.ELEARNING_DATA / "course-index.json"

def get(path, **q):
    req = urllib.request.Request(f"{BASE}{path}?{urllib.parse.urlencode(q)}", headers={"Authorization": f'MediaBrowser Token="{KEY}"'})
    with urllib.request.urlopen(req) as r:
        return json.load(r)

user = next(u for u in get("/Users") if u["Policy"]["IsAdministrator"])["Id"]
series = {s["Path"]: s for s in get("/Items", userId=user, includeItemTypes="Series", recursive="true",
                                    fields="Path,ProductionYear,Studios")["Items"]}
problems, checked = [], 0
for entry in json.loads(INDEX.read_text()):
    path, course = entry["Path"], entry["Course"]
    name = course["Title"]
    s = series.get(path)
    if s is None:
        problems.append(f"{name}: no Series in Jellyfin"); continue
    def bad(msg): problems.append(f"{name}: {msg}")
    if s["Name"] != course["Title"]: bad(f"title {s['Name']!r}")
    if course["Studio"] and [x["Name"] for x in s.get("Studios", [])] != [course["Studio"]]: bad(f"studios {s.get('Studios')}")
    if course["Year"] and s.get("ProductionYear") != course["Year"]: bad(f"year {s.get('ProductionYear')}")
    if "Primary" not in s.get("ImageTags", {}): bad("no course artwork")

    seasons = get(f"/Shows/{s['Id']}/Seasons", userId=user, fields="Path")["Items"]
    expected = [x for x in course["Sections"] if x["SeasonNumber"] is not None]
    got = [(x.get("IndexNumber"), x["Name"]) for x in seasons]
    want = [(x["SeasonNumber"], x["Title"]) for x in expected]
    if got != want: bad(f"seasons {got} != {want}")

    episodes = get(f"/Shows/{s['Id']}/Episodes", userId=user, fields="Path,MediaStreams")["Items"]
    by_path = {}
    for e in episodes:
        by_path.setdefault(e["Path"], []).append(e)
    lectures = [(x, l) for x in expected for l in x["Lectures"] if l["EpisodeNumber"] is not None]
    if len(episodes) != len(lectures): bad(f"{len(episodes)} episodes for {len(lectures)} video lectures")
    for section, lecture in lectures:
        found = by_path.get(f"{path}/{lecture['RelativePath']}", [])
        checked += 1
        if len(found) != 1:
            bad(f"{lecture['RelativePath']}: {len(found)} episodes"); continue
        e = found[0]
        actual = (e.get("ParentIndexNumber"), e.get("IndexNumber"), e["Name"])
        if actual != (section["SeasonNumber"], lecture["EpisodeNumber"], lecture["Title"]):
            bad(f"{lecture['RelativePath']}: S{actual[0]}E{actual[1]} {actual[2]!r}")
        subs = [m for m in e.get("MediaStreams", []) if m["Type"] == "Subtitle" and m.get("IsExternal")]
        if len(subs) < len(lecture["Subtitles"]):
            bad(f"{lecture['RelativePath']}: {len(subs)}/{len(lecture['Subtitles'])} subtitles "
                f"(langs {[m.get('Language') for m in subs]})")

print(f"checked {len(series)} series, {checked} lectures")
for p in problems:
    print("  MISMATCH", p)
print("OK" if not problems else f"{len(problems)} mismatches")
sys.exit(1 if problems else 0)
