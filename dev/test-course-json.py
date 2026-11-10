#!/usr/bin/env python3
"""Live check of course.json on the dev instance (docs/course-json.md).

Creates a small mock course next to the fixtures, then walks it through: no course.json (parsed
names) -> course.json with overrides -> edited course.json -> invalid course.json -> removed, and
after each step scans the library and compares what Jellyfin (series, seasons, episodes) and the
/ELearning outline show with what the file says. Also checks the existing fixture that ships a
course.json. The mock course is removed at the end (--keep leaves it).
Needs the plugin installed and the "Course Fixtures" library ticked (what `docker compose up` sets up).
Usage: dev/test-course-json.py [--keep]"""
import json, os, re, shutil, sys, time, urllib.error, urllib.parse, urllib.request
import devenv  # loads dev/.env (or DEV_ENV_FILE)

BASE = os.environ.get("JELLYFIN_URL", "http://localhost:8096")
KEY = os.environ["JELLYFIN_API_KEY"]
COURSES = devenv.STATE / "courses"
NAME = "Mock Course Alpha"
COURSE = COURSES / NAME
CATALOG_TTL = 31  # seconds: the plugin parses a course at most every 30 s per scan
KEEP = "--keep" in sys.argv

def request(method, path, body=None, **query):
    url = f"{BASE}{path}" + (f"?{urllib.parse.urlencode(query)}" if query else "")
    h = {"Content-Type": "application/json", "Authorization": f'MediaBrowser Client="cj-test", Device="script", DeviceId="cj-test", Version="1.0", Token="{KEY}"'}
    req = urllib.request.Request(url, method=method, headers=h, data=None if body is None else json.dumps(body).encode())
    try:
        with urllib.request.urlopen(req) as r:
            data = r.read()
            return json.loads(data) if data else None
    except urllib.error.HTTPError as e:
        raise SystemExit(f"{method} {path} -> {e.code} {e.read()[:200]!r}")

USER_ID = request("GET", "/Users")[0]["Id"]  # the API key has no user; the outline is per user

results = []
def check(name, ok, detail=""):
    results.append(ok)
    print(f"{'PASS' if ok else 'FAIL'}  {name}" + ("" if ok else f"  ({detail})"))

def items(kind, **query):
    return request("GET", "/Items", recursive="true", includeItemTypes=kind, fields="Path,Overview,Studios", **query)["Items"]

def shows(series_id, what):
    return request("GET", f"/Shows/{series_id}/{what}", userId=USER_ID, fields="Overview")["Items"]

def snapshot(path):
    """What Jellyfin and the plugin show for the course at `path` (None until it is scanned)."""
    series = next((s for s in items("Series") if s["Path"] == path), None)
    if series is None:
        return None
    seasons = sorted(shows(series["Id"], "Seasons"), key=lambda s: s.get("IndexNumber", -1))
    episodes = sorted(shows(series["Id"], "Episodes"), key=lambda e: (e.get("ParentIndexNumber", -1), e.get("IndexNumber", -1)))
    outline = request("GET", f"/ELearning/Courses/{series['Id']}/Outline", userId=USER_ID)
    return {
        "course": (series["Name"], series.get("Overview"), [x["Name"] for x in series.get("Studios", [])], series.get("ProductionYear")),
        "seasons": [(s.get("IndexNumber"), s["Name"], s.get("Overview")) for s in seasons],
        "episodes": [(e.get("ParentIndexNumber"), e.get("IndexNumber"), e["Name"], e.get("Overview")) for e in episodes],
        "outline": (outline["Title"], [(s["Title"], [l["Title"] for l in s["Lectures"]]) for s in outline["Sections"]], [r["Name"] for r in outline["Resources"]]),
    }

def scan_until(path, predicate, timeout=240):
    """Waits out the catalog cache, scans the library, and polls until predicate(snapshot) holds."""
    time.sleep(CATALOG_TTL)
    request("POST", "/Library/Refresh")
    deadline, last = time.time() + timeout, None
    while time.time() < deadline:
        time.sleep(3)
        last = snapshot(path)
        if last is not None and predicate(last):
            return last
    return last

def write_json(text):
    (COURSE / "course.json").write_text(text)

# --- the fixture that ships a course.json (dev/make-fixtures.sh) -------------------------------
fixture = snapshot("/data/courses/Sample - Markdown Notes")
check("fixture 'Sample - Markdown Notes' is in the library", fixture is not None)
if fixture:
    check("fixture: course title and description", fixture["course"][:2] == ("Markdown Notes (course.json)", "Title, description and section order come from course.json."), fixture["course"])
    check("fixture: sections renamed and reordered", [(n, t) for n, t, _ in fixture["seasons"]] == [(1, "Further"), (2, "Basics")], fixture["seasons"])
    check("fixture: section description", fixture["seasons"][0][2] == "Shown first.", fixture["seasons"])
    check("fixture: lecture renamed and described", ("Hello!", "Overridden description.") in [(e[2], e[3]) for e in fixture["episodes"]], fixture["episodes"])
    check("fixture: course.json is not a resource", "course.json" not in fixture["outline"][2], fixture["outline"][2])

# --- the mock course ---------------------------------------------------------------------------
clip = next(COURSES.rglob("*.mp4"), None)
if clip is None:
    raise SystemExit(f"no fixture clip under {COURSES}: run dev/make-fixtures.sh")
shutil.rmtree(COURSE, ignore_errors=True)
for rel in ["01 Basics/01 Intro.mp4", "01 Basics/02 Setup.mp4", "02 Advanced/03 Deep dive.mp4", "03 Bonus/04 Extra.mp4"]:
    (COURSE / rel).parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(clip, COURSE / rel)
(COURSE / "notes.pdf").write_bytes(b"%PDF-1.1\n")
path = f"/data/courses/{NAME}"

try:
    print("\n-- 1. no course.json: names come from the folders")
    base = scan_until(path, lambda s: len(s["episodes"]) == 4)
    check("scanned", base is not None and len(base["episodes"]) == 4, base)
    base_seasons = [(n, t) for n, t, _ in base["seasons"]]
    check("parsed section names and order", base_seasons == [(1, "Basics"), (2, "Advanced"), (3, "Bonus")], base_seasons)
    check("parsed lecture names", [e[2] for e in base["episodes"]] == ["Intro", "Setup", "Deep dive", "Extra"], base["episodes"])
    check("no description", base["course"][1] is None and all(s[2] is None for s in base["seasons"]), base["course"])

    print("\n-- 2. course.json with overrides")
    write_json("""{
      // comments and trailing commas are fine
      "title": "Overridden Course", "description": "Course text.", "studio": "Mock Studio", "year": 2021,
      "sections": {
        "01 Basics": { "title": "Foundations", "description": "Section text.", "order": 3 },
        "03 Bonus": { "order": 0.5 },
      },
      "lectures": {
        "01 Basics/01 Intro.mp4": { "title": "Welcome", "description": "Lecture text." },
        "01 Basics/02 Setup.mp4": { "order": 0 },
      }
    }""")
    want_seasons = [(1, "Bonus"), (2, "Advanced"), (3, "Foundations")]
    got = scan_until(path, lambda s: s["course"][0] == "Overridden Course" and [(n, t) for n, t, _ in s["seasons"]] == want_seasons)
    check("course title, description, studio, year", got["course"] == ("Overridden Course", "Course text.", ["Mock Studio"], 2021), got["course"])
    check("sections renumbered by order (0.5 < 2 < 3)", [(n, t) for n, t, _ in got["seasons"]] == want_seasons, got["seasons"])
    check("section description", got["seasons"][2][2] == "Section text.", got["seasons"])
    eps = {e[2]: e for e in got["episodes"]}
    check("lecture renamed + described", "Welcome" in eps and eps["Welcome"][3] == "Lecture text.", got["episodes"])
    check("lecture order: Setup (order 0) before Welcome in Foundations", eps.get("Setup", (0, 0))[:2] == (3, 1) and eps.get("Welcome", (0, 0))[:2] == (3, 2), got["episodes"])
    check("unlisted lecture keeps its parsed name", {"Deep dive", "Extra"} <= set(eps), list(eps))
    check("outline: title, sections, lectures", got["outline"][:2] == ("Overridden Course", [("Bonus", ["Extra"]), ("Advanced", ["Deep dive"]), ("Foundations", ["Setup", "Welcome"])]), got["outline"])
    check("outline: course.json is not a resource (only the PDF is)", [r.lower() for r in got["outline"][2]] == ["notes.pdf"], got["outline"][2])

    print("\n-- 3. edited course.json")
    write_json('{ "title": "Edited Title", "sections": { "02 Advanced": { "title": "Harder" } } }')
    got = scan_until(path, lambda s: s["course"][0] == "Edited Title")
    check("new title and section name, old overrides gone", got["course"][0] == "Edited Title" and [(n, t) for n, t, _ in got["seasons"]] == [(1, "Basics"), (2, "Harder"), (3, "Bonus")], (got["course"], got["seasons"]))
    print(f"NOTE  description after it was removed from the file: {got['course'][1]!r} (a removed description is not cleared)")

    print("\n-- 4. invalid course.json is ignored")
    write_json("{ nope")
    got = scan_until(path, lambda s: s["course"][0] == NAME)
    print(f"NOTE  studio/year/descriptions left over: {got['course'][1:]} (values are only written, never cleared)")
    check("falls back to parsed names", got["course"][0] == NAME and [t for _, t, _ in got["seasons"]] == ["Basics", "Advanced", "Bonus"], got["course"])
    logs = sorted((devenv.STATE / "config/log").glob("log_*.log"))
    logged = any(re.search(r"Ignoring .*course\.json", p.read_text(errors="replace")) for p in logs)
    check("warning in the Jellyfin log", logged, [p.name for p in logs])

    print("\n-- 5. course.json removed")
    (COURSE / "course.json").unlink()
    got = scan_until(path, lambda s: s["episodes"] and s["episodes"][0][2] == "Intro")
    check("names are the parsed ones", [e[2] for e in got["episodes"]] == ["Intro", "Setup", "Deep dive", "Extra"], got["episodes"])
finally:
    if not KEEP:
        shutil.rmtree(COURSE, ignore_errors=True)
        request("POST", "/Library/Refresh")

print(f"\n{sum(results)}/{len(results)} checks passed")
sys.exit(0 if all(results) else 1)
