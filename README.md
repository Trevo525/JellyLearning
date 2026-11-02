# Jellyfin E-Learning Plugin

Watch exported e-learning courses (Udemy, Skillshare, Dometrain, Craftsy, …) in Jellyfin like on
the course site: sections and lectures in course order, exact resume positions, course progress,
and viewable/downloadable resources. See [PLAN.md](PLAN.md) for the design and roadmap.

Targets **Jellyfin 12.2** (.NET 10). Requires the
[File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin.

## Development

```bash
docker compose -f dev/docker-compose.yml up -d   # dev Jellyfin at http://localhost:8096
./dev/deploy.sh                                  # build, install into dev instance, restart
dotnet test                                      # parser/index unit + snapshot tests
./dev/make-fixtures.sh                           # tiny test courses -> /data/courses
```

First-run setup of the dev instance is in [dev/README.md](dev/README.md).

`dev/ui-view.py` prints every course as jellyfin-web lists it (needs `JELLYFIN_API_KEY` in
`dev/.env`); `dev/library-report.sh` dumps the raw database view.
