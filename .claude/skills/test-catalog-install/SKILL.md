---
name: test-catalog-install
description: Check that JellyLearning and File Transformation install from the plugin repository manifest on a throwaway empty Jellyfin 12.2 container. Use after a release, after changing manifest.json, build/package.sh or build/update-manifest.py, or when asked to test installing from the catalog or manifest.
---

# Test the plugin catalog install

Runs `dev/test-catalog-install.py`: starts an empty Jellyfin 12.2 (no plugins), finishes its wizard, adds the
manifest as a repository, installs everything it lists for 12.2, restarts, and checks each plugin is Active.
It removes its containers, network and state afterwards (`--keep` leaves them, login `admin` / `catalog-test`).

```bash
dev/test-catalog-install.py                       # the published manifest on GitHub (after a release)
dev/test-catalog-install.py --serve DIR           # a local manifest.json + zips in DIR, before publishing
dev/test-catalog-install.py --port 8099           # if 8098 is taken
```

Takes about two minutes. Run it in the background (`run_in_background`) or give the command a long timeout.

## Reading the result

- `OK: all catalog plugins installed and Active` is a pass. "course UI registered" confirms File Transformation
  picked up the plugin's web script.
- `did not install`: usually a checksum or URL problem. Jellyfin needs an **MD5** (upper-case hex) of the exact zip,
  and the plugin files at the **root** of the zip. `docker logs jl-catalog-jellyfin` (use `--keep`) names the cause.
- The catalog lists no package: the manifest is unreachable, or no version has `targetAbi` 12.2.0.0. A private
  GitHub repo returns 404 for the raw URL.

## Local copy of a manifest

Containers on this machine cannot reach the host (`host.docker.internal` times out and Jellyfin hangs on
`GET /Packages` for 100 s). `--serve DIR` therefore serves DIR from a sibling container on the test's own docker
network; the `sourceUrl`s inside the manifest must then point at `http://jl-catalog-repo:8099/<zip>`.
To build such a copy: `build/package.sh vX.Y.Z`, copy `manifest.json` and the zip into DIR, run
`build/update-manifest.py vX.Y.Z <zip> DIR/manifest.json`, then rewrite the JellyLearning `sourceUrl` host.

Don't touch other containers: this machine runs unrelated ones. The script only removes `jl-catalog-*`.
