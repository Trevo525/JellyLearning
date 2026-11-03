# Jellyfin E-Learning Plugin

Watch exported e-learning courses (Udemy, Skillshare, Dometrain, Craftsy, …) in Jellyfin like on
the course site: sections and lectures in course order, exact resume positions, course progress,
and viewable/downloadable resources. See [PLAN.md](PLAN.md) for the design and roadmap.

Targets **Jellyfin 12.2** (.NET 10). Requires the
[File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin.

## Server setup

1. Install the File Transformation plugin and this plugin, restart.
2. Create a **Shows** library for the courses (one folder per course). In its settings turn off
   all internet metadata/image fetchers (TheMovieDb, OMDb) and the NFO saver, and under the
   image fetchers for **Shows** and **Seasons** enable **E-Learning first lecture**.
3. Dashboard → Plugins → E-Learning → tick the library under *Course libraries*, save, then
   scan the library (Dashboard → Libraries → Scan all libraries).
4. Reload the web client once (the course UI is injected into it by File Transformation):
   course, section and lecture pages then show progress, the full outline and resources, and
   **Start/Continue course** opens the Udemy-style course player.
5. Optional: Dashboard → Playback → Resume → *Max resume percentage* = 100. The plugin enforces
   its own completion rule for course lectures regardless (settings page: "Lecture completion");
   this only avoids a brief "watched" flicker while a lecture plays.

## Development

```bash
docker compose -f dev/docker-compose.yml up -d   # dev Jellyfin at http://localhost:8096
./dev/deploy.sh                                  # build, install into dev instance, restart
dotnet test                                      # unit + snapshot tests
./dev/make-fixtures.sh                           # tiny test courses -> /data/courses
```

First-run setup of the dev instance is in [dev/README.md](dev/README.md).

With `JELLYFIN_API_KEY` in `dev/.env` (`set -a; . dev/.env; set +a`):
`dev/verify-library.py` checks every course in Jellyfin against the parser and
`dev/test-progress.py` checks the completion rule with simulated playback and
`dev/test-api.py` checks the `/ELearning` API (run all three after each change and after server
upgrades); `dev/ui-view.py` prints courses as jellyfin-web lists them;
`dev/library-report.sh` dumps the raw database view.

## Third-party

The course player bundles [hls.js](https://github.com/video-dev/hls.js) 1.7.4 (light build,
Apache-2.0, `Jellyfin.Plugin.ELearning/Web/ThirdParty/`, license next to it), and its icons
are Material Icons paths (Apache-2.0).
