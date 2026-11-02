# Dev Jellyfin instance

Throwaway Jellyfin **12.2** for testing the plugin. Runtime state lives in `dev/jellyfin/`
(gitignored); delete that folder to start fresh.

```bash
docker compose -f dev/docker-compose.yml up -d     # start
docker compose -f dev/docker-compose.yml logs -f   # logs
docker compose -f dev/docker-compose.yml down      # stop
```

## First-run setup (once per fresh `dev/jellyfin/`)

1. Open http://localhost:8096 and complete the startup wizard (a local test admin account).
2. Dashboard → Plugins → Repositories → add
   `https://www.iamparadox.dev/jellyfin/plugins/manifest.json`
3. Catalog → install **File Transformation** (3.0.3.0 has a 12.2.0.0 build) → restart the server.
4. Dashboard → Playback → Resume → *Max resume percentage* = 100 (see PLAN.md, progress tracking).
5. Add a **Shows** library pointing at `/data/courses` (host: `dev/jellyfin/courses/`).

## Version pin

The image is pinned to `version-12.2ubu2604` because `:latest` is already 12.4 and the plugin's
Jellyfin.Controller/Model package versions must match the server. Bump both together.
Don't downgrade an existing config folder; newer servers migrate the database irreversibly.
