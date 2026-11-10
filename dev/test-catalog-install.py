#!/usr/bin/env python3
"""Install every plugin in the plugin repository manifest on a throwaway, empty Jellyfin 12.2 and check
that they all load as Active after a restart. Needs Docker; Python standard library only.

  dev/test-catalog-install.py                      # the published manifest on GitHub
  dev/test-catalog-install.py --serve DIR          # a local manifest.json (+ zips) in DIR, served from a
                                                   # sibling container (containers can't reach the host)
  dev/test-catalog-install.py --manifest URL       # any other manifest URL
  options: --port 8098, --keep (leave the containers running to look around; admin / catalog-test)

Everything it creates is named jl-catalog-*; it is removed again unless --keep is given."""
import argparse
import json
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request

IMAGE = "lscr.io/linuxserver/jellyfin:version-12.2ubu2604"  # same pin as dev/compose.yaml
DEFAULT_MANIFEST = "https://raw.githubusercontent.com/Trevo525/JellyLearning/main/manifest.json"
ABI = "12.2.0.0"
NETWORK, SERVER, REPO = "jl-catalog-net", "jl-catalog-jellyfin", "jl-catalog-repo"
USER, PASSWORD = "admin", "catalog-test"
CLIENT = 'MediaBrowser Client="catalog-test", Device="script", DeviceId="jl-catalog-test", Version="1"'

args = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
args.add_argument("--manifest", default=DEFAULT_MANIFEST)
args.add_argument("--serve", metavar="DIR")
args.add_argument("--port", type=int, default=8098)
args.add_argument("--keep", action="store_true")
opts = args.parse_args()

base = f"http://localhost:{opts.port}"
token = None


def docker(*cmd, check=True):
    return subprocess.run(["docker", *cmd], check=check, capture_output=True, text=True).stdout.strip()


def call(method, path, body=None, auth=True, retries=60):
    header = CLIENT + (f', Token="{token}"' if auth and token else "")
    data = json.dumps(body).encode() if body is not None else (b"" if method == "POST" else None)
    for _ in range(retries):
        request = urllib.request.Request(base + path, data=data, method=method,
                                         headers={"Authorization": header, "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(request, timeout=120) as response:
                raw = response.read()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as e:
            if e.code != 503:  # 503: still starting up
                raise SystemExit(f"{method} {path} -> {e.code} {e.read()[:300]!r}")
        except OSError:
            pass
        time.sleep(2)
    raise SystemExit(f"{method} {path}: no answer")


def wait_for_server():
    for _ in range(90):
        try:
            info = call("GET", "/System/Info/Public", auth=False, retries=1)
            if info and "StartupWizardCompleted" in info:  # answers with a startup page before it is ready
                return info
        except SystemExit:
            pass
        time.sleep(2)
    raise SystemExit("Jellyfin did not come up")


def cleanup(state):
    for name in (SERVER, REPO):
        docker("rm", "-f", name, check=False)
    docker("network", "rm", NETWORK, check=False)
    shutil.rmtree(state, ignore_errors=True)


def main():
    global token
    for name in (SERVER, REPO):  # leftovers of an earlier run
        docker("rm", "-f", name, check=False)
    docker("network", "rm", NETWORK, check=False)
    state = tempfile.mkdtemp(prefix="jl-catalog-")
    manifest_url = opts.manifest
    try:
        docker("network", "create", NETWORK)
        if opts.serve:
            docker("run", "-d", "--name", REPO, "--network", NETWORK, "-v", f"{opts.serve}:/srv:ro", "-w", "/srv",
                   "python:3.13-slim", "python", "-m", "http.server", "8099")
            manifest_url = f"http://{REPO}:8099/manifest.json"
        docker("run", "-d", "--name", SERVER, "--network", NETWORK, "-e", "PUID=1000", "-e", "PGID=1000",
               "-v", f"{state}/config:/config", "-p", f"{opts.port}:8096", IMAGE)
        print(f"Jellyfin starting at {base} (empty, no plugins); manifest {manifest_url}")
        wait_for_server()

        call("POST", "/Startup/Configuration", {"UICulture": "en-US", "MetadataCountryCode": "US", "PreferredMetadataLanguage": "en"}, auth=False)
        call("GET", "/Startup/User", auth=False)
        call("POST", "/Startup/User", {"Name": USER, "Password": PASSWORD}, auth=False)
        call("POST", "/Startup/RemoteAccess", {"EnableRemoteAccess": True, "EnableAutomaticPortMapping": False}, auth=False)
        call("POST", "/Startup/Complete", auth=False)
        token = call("POST", "/Users/AuthenticateByName", {"Username": USER, "Pw": PASSWORD}, auth=False)["AccessToken"]

        call("POST", "/Repositories", [{"Name": "catalog-test", "Url": manifest_url, "Enabled": True}])
        packages = [p for p in call("GET", "/Packages") if any(v["targetAbi"] == ABI for v in p["versions"])]
        if not packages:
            raise SystemExit(f"FAIL: the catalog lists no package for {ABI}")
        for package in packages:
            version = max((v for v in package["versions"] if v["targetAbi"] == ABI), key=lambda v: v["timestamp"])
            print(f"  catalog: {package['name']} {version['version']} ({version['sourceUrl']})")
            query = urllib.parse.urlencode({"assemblyGuid": package["guid"].replace("-", ""), "version": version["version"],
                                            "repositoryUrl": manifest_url})
            call("POST", "/Packages/Installed/" + urllib.parse.quote(package["name"]) + "?" + query)
            for _ in range(60):  # the download runs in the background; the plugin is listed once it is on disk
                if any(p["Id"].replace("-", "") == package["guid"].replace("-", "") for p in call("GET", "/Plugins")):
                    break
                time.sleep(2)
            else:
                raise SystemExit(f"FAIL: {package['name']} did not install (a wrong checksum is logged by Jellyfin: docker logs {SERVER})")
            print(f"  installed {package['name']}")

        call("POST", "/System/Restart")
        time.sleep(6)
        wait_for_server()
        time.sleep(10)
        plugins = {p["Name"]: p for p in call("GET", "/Plugins")}
        failed = [p["name"] for p in packages if plugins.get(p["name"], {}).get("Status") != "Active"]
        for package in packages:
            plugin = plugins.get(package["name"], {})
            print(f"  {package['name']}: {plugin.get('Version', 'missing')} {plugin.get('Status', '')}")
        if failed:
            raise SystemExit(f"FAIL: not Active after the restart: {', '.join(failed)} (docker logs {SERVER})")
        log = docker("logs", SERVER, check=False)
        print("OK: all catalog plugins installed and Active" +
              ("; course UI registered" if "Registered the e-learning course UI" in log else ""))
        if opts.keep:
            print(f"kept: {base} ({USER} / {PASSWORD}), state in {state}; remove with: docker rm -f {SERVER} {REPO}; docker network rm {NETWORK}")
            state = None
    finally:
        if state:
            cleanup(state)


if __name__ == "__main__":
    sys.exit(main())
