#!/usr/bin/env python3
"""Print each course exactly as jellyfin-web lists it: Shows/{id}/Seasons, then
Shows/{id}/Episodes per season (same endpoints and order the web client uses).
Usage: dev/ui-view.py [name-substring]   (needs JELLYFIN_API_KEY from dev/.env)"""
import json, os, sys, urllib.request, urllib.parse

BASE = os.environ.get("JELLYFIN_URL", "http://localhost:8096")
KEY = os.environ["JELLYFIN_API_KEY"]

def get(path, **q):
    url = f"{BASE}{path}?{urllib.parse.urlencode(q)}"
    req = urllib.request.Request(url, headers={"Authorization": f'MediaBrowser Token="{KEY}"'})
    with urllib.request.urlopen(req) as r:
        return json.load(r)

user = next(u for u in get("/Users") if u["Policy"]["IsAdministrator"])["Id"]
flt = sys.argv[1].lower() if len(sys.argv) > 1 else ""
series = get("/Items", userId=user, includeItemTypes="Series", recursive="true",
             fields="Path,ProductionYear,Studios,SortName", sortBy="SortName")["Items"]
for s in series:
    if flt not in s["Name"].lower():
        continue
    print(f"### {s['Name']}  [sort={s.get('SortName')!r} year={s.get('ProductionYear')} "
          f"studios={[x['Name'] for x in s.get('Studios', [])]}]")
    for se in get(f"/Shows/{s['Id']}/Seasons", userId=user, fields="Path")["Items"]:
        eps = get(f"/Shows/{s['Id']}/Episodes", userId=user, seasonId=se["Id"], fields="Path")["Items"]
        print(f"  [S{se.get('IndexNumber', '?')}] {se['Name']!r}  ({len(eps)} eps)")
        for e in eps:
            rel = e.get("Path", "").split(s["Path"] + "/", 1)[-1]
            print(f"      S{e.get('ParentIndexNumber', '?')}E{e.get('IndexNumber', '?')}  {e['Name']!r}  <- {rel}")
