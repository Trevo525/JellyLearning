#!/usr/bin/env python3
"""Set up the dev Jellyfin for the plugin; the `setup` service of dev/compose.yaml runs it on
every `up`. Only adds what is missing, so it never changes settings made by hand:

- startup wizard (admin login generated, unless JELLYFIN_ADMIN_USER/PASSWORD are set)
- an API key for the dev scripts
- File Transformation (needed for the course UI) from its plugin repository, then a restart
- "Course Fixtures" library (/data/courses) and, if a source folder is mounted, "Courses"
  (/data/elearning): Shows libraries without internet metadata, ticked in the plugin settings
- a library scan

Writes JELLYFIN_API_KEY, the admin login and the test user's login (JELLYFIN_TEST_USER /
JELLYFIN_TEST_PASSWORD; the test scripts create that user) to SETUP_ENV_FILE (dev/.env),
keeping everything else in the file. Needs only the standard library."""
import json, os, secrets, sys, time, urllib.error, urllib.parse, urllib.request
from pathlib import Path

BASE = os.environ.get("JELLYFIN_URL", "http://localhost:8096").rstrip("/")
ENV_FILE = Path(os.environ.get("SETUP_ENV_FILE", Path(__file__).resolve().parent / ".env"))
CLIENT = 'MediaBrowser Client="E-Learning dev setup", Device="compose", DeviceId="elearning-dev-setup", Version="1.0"'
APP_NAME = "E-Learning dev scripts"
FILE_TRANSFORMATION = {"name": "File Transformation", "guid": "5e87cc92571a4d8d8d98d2d4147f9f90", "version": "3.0.3.0",
                       "repo": "https://www.iamparadox.dev/jellyfin/plugins/manifest.json", "repo_name": "iamparadox"}
ELEARNING_ID = "092fc611a1984746b4dbc1d2cca7ddc3"
LIBRARIES = [("Course Fixtures", "/data/courses"), ("Courses", "/data/elearning")]
token = None


def call(method, path, body=None, ok=(200, 204), auth=True):
    header = CLIENT + (f', Token="{token}"' if auth and token else "")
    data = json.dumps(body).encode() if body is not None else (b"" if method == "POST" else None)
    req = urllib.request.Request(BASE + path, data=data, method=method,
                                 headers={"Authorization": header, "Content-Type": "application/json"})
    for attempt in range(90):
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                raw = r.read()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as e:
            if e.code == 503 and attempt < 89:  # still starting up (also right after a restart)
                time.sleep(2)
                continue
            if e.code in ok:
                return None
            raise SystemExit(f"{method} {path} -> {e.code} {e.read()[:300]!r}")


def wait_for_server(timeout=300):
    end = time.time() + timeout
    while time.time() < end:
        try:
            info = call("GET", "/System/Info/Public", auth=False)
            if info:
                return info
        except (urllib.error.URLError, OSError, SystemExit):
            pass
        time.sleep(2)
    raise SystemExit(f"Jellyfin did not answer at {BASE} within {timeout} s")


def read_env():
    values = {}
    if ENV_FILE.exists():
        for line in ENV_FILE.read_text().splitlines():
            key, sep, value = line.partition("=")
            if sep and not key.lstrip().startswith("#"):
                values[key.strip()] = value.strip()
    return values


def write_env(updates):
    lines = ENV_FILE.read_text().splitlines() if ENV_FILE.exists() else []
    seen = set()
    for i, line in enumerate(lines):
        key = line.partition("=")[0].strip()
        if key in updates:
            lines[i] = f"{key}={updates[key]}"
            seen.add(key)
    if any(k not in seen for k in updates) and not any(l.startswith("# Written by dev/setup.py") for l in lines):
        lines.append("# Written by dev/setup.py (compose `setup` service); edit freely, it only fills in what is missing.")
    lines += [f"{k}={v}" for k, v in updates.items() if k not in seen]
    ENV_FILE.write_text("\n".join(lines) + "\n")


def key_works(key):
    global token
    token, previous = key, token
    try:
        call("GET", "/System/Info", ok=())
        return True
    except SystemExit:
        token = previous
        return False


def startup_wizard(env):
    user = env.get("JELLYFIN_ADMIN_USER") or "admin"
    password = env.get("JELLYFIN_ADMIN_PASSWORD") or secrets.token_urlsafe(12)
    call("POST", "/Startup/Configuration", {"UICulture": "en-US", "MetadataCountryCode": "US", "PreferredMetadataLanguage": "en"}, auth=False)
    call("GET", "/Startup/User", auth=False)
    call("POST", "/Startup/User", {"Name": user, "Password": password}, auth=False)
    call("POST", "/Startup/RemoteAccess", {"EnableRemoteAccess": True, "EnableAutomaticPortMapping": False}, auth=False)
    call("POST", "/Startup/Complete", auth=False)
    print(f"startup wizard done, admin user {user!r}")
    return {"JELLYFIN_ADMIN_USER": user, "JELLYFIN_ADMIN_PASSWORD": password}


def log_in(env):
    global token
    user, password = env.get("JELLYFIN_ADMIN_USER"), env.get("JELLYFIN_ADMIN_PASSWORD")
    if not user or password is None:
        raise SystemExit(f"Jellyfin is already set up, but {ENV_FILE} has no working JELLYFIN_API_KEY and no "
                         "JELLYFIN_ADMIN_USER / JELLYFIN_ADMIN_PASSWORD. Add one of them (or delete the dev state).")
    token = call("POST", "/Users/AuthenticateByName", {"Username": user, "Pw": password}, auth=False)["AccessToken"]


def api_key():
    keys = call("GET", "/Auth/Keys")["Items"]
    mine = [k for k in keys if k.get("AppName") == APP_NAME]
    if not mine:
        call("POST", "/Auth/Keys?" + urllib.parse.urlencode({"app": APP_NAME}))
        mine = [k for k in call("GET", "/Auth/Keys")["Items"] if k.get("AppName") == APP_NAME]
        print("created an API key for the dev scripts")
    return mine[-1]["AccessToken"]


def install_file_transformation():
    if any(p["Id"].replace("-", "") == FILE_TRANSFORMATION["guid"] for p in call("GET", "/Plugins")):
        return False
    repos = call("GET", "/Repositories")
    if not any(r["Url"] == FILE_TRANSFORMATION["repo"] for r in repos):
        call("POST", "/Repositories", repos + [{"Name": FILE_TRANSFORMATION["repo_name"], "Url": FILE_TRANSFORMATION["repo"], "Enabled": True}])
    query = urllib.parse.urlencode({"assemblyGuid": FILE_TRANSFORMATION["guid"], "version": FILE_TRANSFORMATION["version"],
                                    "repositoryUrl": FILE_TRANSFORMATION["repo"]})
    call("POST", "/Packages/Installed/" + urllib.parse.quote(FILE_TRANSFORMATION["name"]) + "?" + query)
    for _ in range(90):  # the download runs in the background; the plugin is listed once it is on disk
        if any(p["Id"].replace("-", "") == FILE_TRANSFORMATION["guid"] for p in call("GET", "/Plugins")):
            break
        time.sleep(2)
    else:
        raise SystemExit(f"{FILE_TRANSFORMATION['name']} did not install; see `docker compose -f dev/compose.yaml logs jellyfin`")
    print(f"installed {FILE_TRANSFORMATION['name']} {FILE_TRANSFORMATION['version']}")
    return True


def restart():
    call("POST", "/System/Restart")
    time.sleep(5)
    wait_for_server()
    for _ in range(60):  # /System/Info/Public answers before plugins and the API are ready
        try:
            call("GET", "/System/Info", ok=())
            return
        except SystemExit:
            time.sleep(2)


def library_options():
    def fetchers(kind, images):
        return {"Type": kind, "MetadataFetchers": [], "MetadataFetcherOrder": [], "ImageFetchers": images, "ImageFetcherOrder": images}
    return {"EnableRealtimeMonitor": True, "EnableInternetProviders": False, "SaveLocalMetadata": False, "MetadataSavers": [],
            "EnableChapterImageExtraction": False, "ExtractChapterImagesDuringLibraryScan": False,
            "TypeOptions": [fetchers("Series", []), fetchers("Season", []),
                            fetchers("Episode", ["Embedded Image Extractor", "Screen Grabber"])]}


def libraries():
    existing = {f["Locations"][0]: f for f in call("GET", "/Library/VirtualFolders") if f.get("Locations")}
    ids = []
    for name, path in LIBRARIES:
        if path not in existing:
            if path == "/data/elearning" and not (os.path.isdir(path) and os.listdir(path)):
                continue  # no real courses mounted (ELEARNING_SOURCE unset); setup sees the same mount
            query = urllib.parse.urlencode({"name": name, "collectionType": "tvshows", "paths": path, "refreshLibrary": "false"})
            call("POST", "/Library/VirtualFolders?" + query, {"LibraryOptions": library_options()})
            print(f"created library {name!r} ({path})")
            existing = {f["Locations"][0]: f for f in call("GET", "/Library/VirtualFolders") if f.get("Locations")}
        ids.append(existing[path]["ItemId"])
    return ids


def tick_course_libraries(ids):
    if not any(p["Id"].replace("-", "") == ELEARNING_ID for p in call("GET", "/Plugins")):
        raise SystemExit("The E-Learning plugin is not loaded; check `docker compose -f dev/compose.yaml logs plugin jellyfin`.")
    config = call("GET", f"/Plugins/{ELEARNING_ID}/Configuration")
    current = {i.replace("-", "") for i in config.get("CourseLibraryIds", [])}
    missing = [i for i in ids if i.replace("-", "") not in current]
    if missing:
        config["CourseLibraryIds"] = config.get("CourseLibraryIds", []) + missing
        call("POST", f"/Plugins/{ELEARNING_ID}/Configuration", config)
        print(f"ticked {len(missing)} course librar{'y' if len(missing) == 1 else 'ies'} in the plugin settings")


def main():
    env = read_env()
    info = wait_for_server()
    updates = {}
    if not info.get("StartupWizardCompleted"):
        updates.update(startup_wizard(env))
        env.update(updates)
    if not (env.get("JELLYFIN_API_KEY") and key_works(env["JELLYFIN_API_KEY"])):
        log_in(env)
        updates["JELLYFIN_API_KEY"] = api_key()
        key_works(updates["JELLYFIN_API_KEY"])
    if not env.get("JELLYFIN_TEST_USER"):
        updates["JELLYFIN_TEST_USER"] = "learner"
    if not env.get("JELLYFIN_TEST_PASSWORD"):
        updates["JELLYFIN_TEST_PASSWORD"] = secrets.token_urlsafe(12)
    if updates:
        write_env(updates)
        print(f"wrote {', '.join(sorted(updates))} to {ENV_FILE}")

    install_file_transformation()
    pending = [p["Name"] for p in call("GET", "/Plugins") if p.get("Status") == "Restart"]
    if pending:
        print(f"restarting Jellyfin to load {', '.join(pending)}")
        restart()
    tick_course_libraries(libraries())
    call("POST", "/Library/Refresh")
    print(f"ready: {os.environ.get('JELLYFIN_HOST_URL', BASE)} (library scan started; logins in {ENV_FILE.name})")


if __name__ == "__main__":
    sys.exit(main())
