#!/usr/bin/env python3
"""End-to-end check of the course completion rule (ProgressGuard) on the dev instance.

Plays fixture lectures as a test user through the same session endpoints jellyfin-web uses
(/Sessions/Playing, /Progress, /Stopped) and checks the stored played state + resume position.
Creates the test user from JELLYFIN_TEST_USER / JELLYFIN_TEST_PASSWORD in dev/.env if missing.
Usage: set -a; . dev/.env; set +a; dev/test-progress.py"""
import json, os, sys, urllib.error, urllib.parse, urllib.request, uuid

BASE = os.environ.get("JELLYFIN_URL", "http://localhost:8096")
KEY, USER, PASSWORD = os.environ["JELLYFIN_API_KEY"], os.environ["JELLYFIN_TEST_USER"], os.environ["JELLYFIN_TEST_PASSWORD"]
TICKS = 10_000_000

def call(method, path, body=None, token=KEY, **query):
    auth = f'MediaBrowser Client="progress-test", Device="script", DeviceId="progress-test", Version="1.0", Token="{token}"'
    req = urllib.request.Request(f"{BASE}{path}?{urllib.parse.urlencode(query)}", method=method,
                                 data=None if body is None else json.dumps(body).encode(),
                                 headers={"Authorization": auth, "Content-Type": "application/json"})
    with urllib.request.urlopen(req) as r:
        data = r.read()
        return json.loads(data) if data else None

if not any(u["Name"] == USER for u in call("GET", "/Users")):
    call("POST", "/Users/New", {"Name": USER, "Password": PASSWORD})
auth = call("POST", "/Users/AuthenticateByName", {"Username": USER, "Pw": PASSWORD}, token="")
token, user_id = auth["AccessToken"], auth["User"]["Id"]

EPISODES = call("GET", "/Items", token=token, userId=user_id, recursive="true", includeItemTypes="Episode", fields="Path")["Items"]

def lecture(path_part):
    return next(i for i in EPISODES if path_part in i["Path"])

def reset(item):
    call("DELETE", f"/UserPlayedItems/{item['Id']}", token=token, userId=user_id)
    call("POST", f"/UserItems/{item['Id']}/UserData", {"PlaybackPositionTicks": 0, "Played": False}, token=token, userId=user_id)

def play(item, *positions):
    """Start, report progress at each position (seconds), stop at the last one."""
    session = uuid.uuid4().hex
    base = {"ItemId": item["Id"], "PlaySessionId": session, "PlayMethod": "DirectPlay", "CanSeek": True}
    call("POST", "/Sessions/Playing", {**base, "PositionTicks": 0}, token=token)
    for p in positions[:-1]:
        call("POST", "/Sessions/Playing/Progress", {**base, "PositionTicks": int(p * TICKS), "EventName": "timeupdate"}, token=token)
    call("POST", "/Sessions/Playing/Stopped", {**base, "PositionTicks": int(positions[-1] * TICKS)}, token=token)
    import time; time.sleep(1.5)  # PlaybackStopped is raised asynchronously
    d = call("GET", f"/UserItems/{item['Id']}/UserData", token=token, userId=user_id)
    return d["Played"], round(d["PlaybackPositionTicks"] / TICKS, 3)

a = lecture("/data/courses/Dometrain - Getting Started - Onabaru in .MOM/02. Onabaru/04. What is onabaru.mp4")
b = lecture("/data/courses/Dometrain - Getting Started - Onabaru in .MOM/02. Onabaru/05. Is onabaru gudinevol.mp4")
# A real lecture of 10+ minutes (from the real sources), for the percentage rule.
c = next((i for i in EPISODES if i["Path"].startswith("/data/elearning/") and i.get("RunTimeTicks", 0) >= 600 * TICKS), None)
runtime = a.get("RunTimeTicks", 0) / TICKS
long_runtime = c["RunTimeTicks"] / TICKS if c else 0
for item in (a, b, c):
    if item: reset(item)
cases = [
    ("stop mid-lecture keeps exact position (Jellyfin: <5 min => played)", lambda: play(a, 6, 12, 15.25), (False, 15.25)),
    ("stop in last 10 s completes, resume resets",                        lambda: play(a, 15, 21), (True, 0.0)),
    ("rewatch of completed lecture stays complete, keeps position",       lambda: play(a, 8.5), (True, 8.5)),
    ("first 5 s are not worth resuming",                                  lambda: play(b, 3), (False, 0.0)),
]
if c:
    p92, p98 = round(long_runtime * 0.92, 2), round(long_runtime * 0.981, 2)
    cases += [
        (f"long lecture stopped at 92 % keeps position (Jellyfin: > MaxResumePct 90)", lambda: play(c, 60, p92), (False, p92)),
        (f"long lecture stopped at 98 % completes", lambda: play(c, p98), (True, 0.0)),
    ]
print(f"fixture lecture {runtime:.0f} s (complete from {runtime - 10:.0f} s); long lecture {long_runtime:.0f} s")
failed = 0
for name, run, expected in cases:
    got = run()
    ok = got == expected
    failed += not ok
    print(f"{'PASS' if ok else 'FAIL'}  {name}: played={got[0]} position={got[1]}s" + ("" if ok else f"  (expected {expected})"))
for item in (a, b, c):
    if item: reset(item)
sys.exit(1 if failed else 0)
