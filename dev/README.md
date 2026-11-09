# Dev Jellyfin instance

Throwaway Jellyfin **12.2** for testing the plugin, built and set up by `dev/compose.yaml`.
Needs only Docker; the dev scripts need Python 3 (standard library only).

```bash
docker compose -f dev/compose.yaml up -d          # build, set up, start: http://localhost:8096
docker compose -f dev/compose.yaml logs -f        # logs (add a service name: plugin, setup, ...)
docker compose -f dev/compose.yaml down           # stop
```

Runtime state lives in `dev/jellyfin/` (gitignored); stop the instance and delete that folder to
start fresh. Logins, the API key and options live in `dev/.env` (gitignored).

## What `up` does

1. **plugin**: builds the plugin in the .NET SDK image (from a copy of the sources, so the repo
   gets no root-owned `bin/`/`obj/`) and installs it as `JellyLearning_0.0.0.0` in Jellyfin's
   plugins folder, removing any other installed version (`dev/build-plugin.sh`).
2. **fixtures**: generates the test courses from the fixture listings into `dev/jellyfin/courses/`
   (= `/data/courses`), only when the listings changed (`dev/make-fixtures.sh --if-changed`).
3. **jellyfin**: starts the server once both are done.
4. **setup** (`dev/setup.py`) only adds what is missing, so settings made by hand stay:
   - completes the startup wizard with the admin user `admin` and a generated password;
   - creates an API key for the dev scripts;
   - adds the File Transformation plugin repository, installs File Transformation 3.0.3.0 (the
     course UI needs it) and restarts Jellyfin;
   - creates the Shows library **Course Fixtures** (`/data/courses`) and, when real courses are
     mounted, **Courses** (`/data/elearning`), with internet metadata, image fetchers and the
     NFO saver off (otherwise courses get matched to random TV shows), and ticks them under
     the plugin's *Course libraries*;
   - writes `JELLYFIN_ADMIN_USER` / `JELLYFIN_ADMIN_PASSWORD`, `JELLYFIN_API_KEY` and the test
     user `JELLYFIN_TEST_USER` / `JELLYFIN_TEST_PASSWORD` (created by the test scripts) to
     `dev/.env`, keeping whatever else is in it;
   - starts a library scan.

Every `up` repeats these steps, so `up -d` after a code change rebuilds and installs the plugin.
For quick iterations `./dev/deploy.sh` does the same and restarts Jellyfin (with the host's
`dotnet` if there is one, otherwise through the `plugin` service).

## Options (`dev/.env`)

| Variable | Default | |
|---|---|---|
| `ELEARNING_SOURCE` | none | A folder of real course exports, mounted read-only at `/data/elearning`; setup adds the "Courses" library for it. |
| `JELLYFIN_PORT` | `8096` | Host port. |
| `PUID` / `PGID` | `1000` | Your user and group (`id -u`, `id -g`): the server and setup run as them, and files the build writes are handed to them. |
| `DEV_STATE` | `./jellyfin` | State folder, relative to `dev/` (for a second instance, together with `docker compose -p <name>` and another port). |

Compose reads `dev/.env` for these, and the dev scripts read it for the logins, so nothing
needs to be exported. Optional by hand: Dashboard → Playback → Resume → *Max resume
percentage* = 100 (see PLAN.md, Phase 4); the test scripts expect the default 90.

Test courses use invented course and file names, never ones from a real library.

## Version pin

The image is pinned to `version-12.2ubu2604` because `:latest` is already 12.4 and the plugin's
Jellyfin.Controller/Model package versions must match the server. Bump both together.
Don't downgrade an existing config folder; newer servers migrate the database irreversibly.

## Dev scripts

Run against the running instance; each reads `dev/.env` itself.

- `dev/verify-library.py`: every course in Jellyfin vs. the parser (exit 1 on mismatch).
- `dev/test-progress.py`: the completion rule end to end, as the test user (created if
  missing). The two long-lecture cases need a real course of 10+ minutes (`ELEARNING_SOURCE`).
- `dev/test-api.py`: the `/ELearning` API end to end (outlines vs. index, files, rendering,
  security headers, completion, refusals); also creates `<test user>-outsider` without library
  access to check access control.
- `dev/ui-view.py [name]`: courses as the web client lists them.
- `dev/library-report.sh [path]`: raw Series/Season/Episode rows from the database (needs `sqlite3`).
- `dev/make-fixtures.sh`: rebuild the test courses by hand (needs `ffmpeg`).

## Inspecting

- Course UI script: `Jellyfin.Plugin.ELearning/Web/elearning.js`; after `./dev/deploy.sh` reload the
  browser tab (the script URL carries a content hash, so no cache clearing is needed). Its
  console messages start with `[JellyLearning]`.
- Document-lecture progress: `dev/jellyfin/config/data/data/elearning/progress/<user>.json`.
- Course index written by the plugin after each scan:
  `dev/jellyfin/config/data/data/elearning/course-index.json`.
- Generated covers: `dev/jellyfin/config/data/data/elearning/covers/`.
