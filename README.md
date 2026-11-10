# JellyLearning

> [!WARNING]
> # ⚠️ Early software: not well tested. Use it carefully.
>
> **This plugin has had very little testing.** It has automated tests (unit and snapshot tests)
> and has been tried by hand on one Jellyfin 12.2 test instance, and that is all. It has not been
> run by other people, on other setups or Jellyfin versions, or on large libraries.
>
> It changes how the items in your course libraries are titled and ordered, and it decides when a
> lecture counts as watched, so expect bugs, and expect that some of its changes to watched state
> and resume positions may be wrong.
>
> - **Back up** Jellyfin's config and database before installing it.
> - **Try it on a test Jellyfin first**, not your main server.
> - Enable it only for a library that holds just your courses.
> - Don't rely on it for anything you can't redo, and please
>   [report problems](https://github.com/Trevo525/JellyLearning/issues).

JellyLearning is a Jellyfin plugin for watching exported e-learning courses (Udemy, Skillshare,
Dometrain, Craftsy, …) like on the course site: sections and lectures in course order, exact
resume positions, course progress, and viewable/downloadable resources. See [PLAN.md](PLAN.md) for the design and roadmap.

Targets **Jellyfin 12.2** (.NET 10). Requires the
[File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin.

## Screenshots

All screenshots show a made-up demo library: invented courses with generated placeholder videos.

**Courses in Jellyfin**: generated covers, and Continue Watching resumes each course at the exact
position you stopped.

![The Jellyfin home page with three demo courses and a Continue Watching row](docs/screenshots/home.png)

**Course page**: progress, course resources (view or download), and the outline of all sections
and lectures, with completion marks and resume positions.

![A course page with a progress bar, resources and the lecture outline](docs/screenshots/course.png)

**Course player**: the course outline beside the video like on the course site, per-lecture
resources, and the lecture resumes where you left it.

![The course player with the outline on the right and a half-watched lecture](docs/screenshots/player.png)

**Readings and quizzes** open inline and count towards the course progress.

![An HTML quiz lecture shown inside the course player](docs/screenshots/quiz.png)

**Resources** open in a viewer (PDF, HTML, Markdown, text, images) or download.

![A PDF resource open in the viewer](docs/screenshots/resource.png)

**Settings**: which libraries are course libraries, and when a lecture counts as complete.

![The plugin settings page](docs/screenshots/settings.png)

## Install

Jellyfin **12.2** only (the plugin is built against 12.2's packages). The web UI needs the
[File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin; this
repository's plugin catalog lists it too, so one repository gives you both.

**From the plugin catalog (recommended)**

1. Dashboard → Plugins → Repositories → add
   `https://raw.githubusercontent.com/Trevo525/JellyLearning/main/manifest.json`.
2. Catalog: install **File Transformation** (3.0.3.0) and **JellyLearning**.
3. Restart Jellyfin.

(File Transformation is listed here as a pointer to its author's release, IAmParadox27's; it is not
rebuilt or changed. The manifest only lists releases that have been published; each tagged release adds
itself to `manifest.json`.)

**By hand**

1. Install File Transformation (3.0.3.0 or later for 12.2) from its own repository
   `https://www.iamparadox.dev/jellyfin/plugins/manifest.json`.
2. Download `JellyLearning_<version>.zip` from a [release](https://github.com/Trevo525/JellyLearning/releases),
   or build it with `build/package.sh <version>`, and unzip it into a new folder in Jellyfin's plugins
   folder, e.g. `<config>/data/plugins/JellyLearning_<version>.0/` (linuxserver image:
   `/config/data/plugins/`; Debian packages: `/var/lib/jellyfin/plugins/`). The zip has the plugin files
   at its root, so the folder must be created first.
3. Restart Jellyfin.

To update by hand, delete the old `JellyLearning_*` folder, unzip the new one, restart. (Releases before
1.1.1 were zipped with a folder inside, and 1.0.0 was packaged as `E-Learning_1.0.0.0`; delete that
folder too when you update.)

The plugin is called **JellyLearning** everywhere you see it: the zip, its folder, Dashboard → Plugins
and the library's metadata fetchers. Only internal names keep the old working title (the
`Jellyfin.Plugin.ELearning` assembly and the `/ELearning` API route).

## Server setup

1. Install File Transformation and JellyLearning (see Install), restart.
2. Create the course library: Dashboard → Libraries → Add Media Library, content type
   **JellyLearning courses**, with one folder per course. It creates a normal Shows library with
   internet metadata, image fetchers and NFO files turned off (otherwise Jellyfin matches courses to
   random TV shows) and adds it to the plugin's course libraries, so you can skip step 3. (Jellyfin's
   list of library types is fixed and plugins can't add to it: the entry is added to the dialog by the
   web UI script, and is a second "Shows" option underneath. If it's missing, create a **Shows**
   library yourself, turn off TheMovieDb/OMDb and the NFO saver, and do step 3.)
   Courses and
   sections get a generated cover (title on a purple/blue gradient); to use your own, put a
   `poster`, `cover` or `folder` image (jpg/png) in the course folder. Covers follow the folder names:
   after renaming a course or section folder, a normal library scan draws the new one.
3. Only for a library you made as plain Shows: Dashboard → Plugins → JellyLearning → tick the
   library under *Course libraries*, save. Then scan the library (Dashboard → Libraries → Scan all libraries).
4. Reload the web client once (the course UI is injected into it by File Transformation):
   course, section and lecture pages then show progress, the full outline and resources, and
   **Start/Continue course** opens the Udemy-style course player.
5. Optional: Dashboard → Playback → Resume → *Max resume percentage* = 100. The plugin enforces
   its own completion rule for course lectures regardless (settings page: "Lecture completion");
   this only avoids a brief "watched" flicker while a lecture plays.

## Overriding titles and order (`course.json`)

Full reference and examples: [docs/course-json.md](docs/course-json.md). The parser works from folder and file names. When those are wrong, put a `course.json` in the
course folder (all keys optional; comments and trailing commas are allowed):

```json
{
  "title": "Course title",
  "description": "Shown as the overview.",
  "studio": "Udemy",
  "year": 2024,
  "sections": {
    "02 Advanced": { "title": "Harder Things", "description": "…", "order": 1 }
  },
  "lectures": {
    "01 Basics/02 Setup.mp4": { "title": "Getting set up", "description": "…", "order": 0.5 }
  }
}
```

- Sections are keyed by their folder, lectures by their main file, both relative to the course
  folder with `/`. Use `"."` for the implicit section of loose lectures.
- `order` replaces the number from the name when sorting (fractions slot in between). It also
  changes season and episode numbers, which are part of Jellyfin's watch-data keys, so set it
  before anyone has progress in the course.
- Titles are used as written. Blank values are ignored. Fields you locked in Jellyfin's metadata
  editor are left alone. A file that is not valid is ignored with a warning in the server log.
- Titles and order follow the file on every scan. Descriptions, `studio` and `year` are only
  written, never cleared: removing one from the file leaves the last value in Jellyfin.

## Development

Needs Docker; the .NET 10 SDK and Python 3 only for the parts you run on the host.

```bash
dotnet test                                      # unit + snapshot tests (no Jellyfin needed)
docker compose -f dev/compose.yaml up -d         # dev Jellyfin at http://localhost:8096, set up
./dev/deploy.sh                                  # rebuild the plugin into it and restart
build/package.sh 0.1.0                           # release zip -> artifacts/release/
```

`docker compose -f dev/compose.yaml up -d` builds the plugin (in a .NET SDK container) into the
dev Jellyfin 12.2's plugins folder, generates the test courses, starts Jellyfin and sets it up:
admin account, File Transformation, a "Course Fixtures" library ticked in the plugin settings,
an API key. The logins and the key end up in `dev/.env`. Details, options (your own courses,
another port or state folder) and the dev scripts are in [dev/README.md](dev/README.md).

Releases: push a tag `vX.Y.Z`; the GitHub Actions workflow (`.github/workflows/build.yml`) runs
the tests, builds the zip with `build/package.sh`, attaches it to a GitHub release and adds the release (URL,
MD5 checksum, changelog) to `manifest.json` on `main` with `build/update-manifest.py`. Every push runs the
tests. The manifest commit exists only on GitHub; pull before you push.

Release checklist:

1. Add the version to the `changelog` in `build.yaml` (it becomes the release notes and the catalog entry);
   `dotnet test`; `build/package.sh X.Y.Z` and check the zip has the plugin files at its root.
2. Tag and push (`git tag -a vX.Y.Z -m "JellyLearning X.Y.Z"`, `git push origin vX.Y.Z`); watch the `test`
   and `release` jobs on GitHub.
3. `git pull`, then check that `manifest.json` on `main` has the new version, and install both plugins from
   `https://raw.githubusercontent.com/Trevo525/JellyLearning/main/manifest.json` on a throwaway Jellyfin 12.2
   with no plugins (see [dev/README.md](dev/README.md), "Testing the plugin catalog").
4. If the release job fails after the tag exists: delete any draft release on GitHub, fix the workflow, move
   the tag (`git tag -f -a …`, `git push -f origin vX.Y.Z`) or tag the next patch version.

Against the running dev instance (they read `dev/.env` themselves):
`dev/verify-library.py` checks every course in Jellyfin against the parser,
`dev/test-progress.py` checks the completion rule with simulated playback and
`dev/test-api.py` checks the `/ELearning` API (run all three after each change and after server
upgrades); `dev/ui-view.py` prints courses as jellyfin-web lists them;
`dev/library-report.sh` dumps the raw database view.

## License

Copyright (C) 2026 Trevor Vance. JellyLearning is free software under the
[GNU General Public License v3.0](LICENSE) (the same family of license as Jellyfin): you may use, change
and share it, and changes you distribute must stay under the GPL.

## Third-party

The course player bundles [hls.js](https://github.com/video-dev/hls.js) 1.7.4 (light build,
Apache-2.0, `Jellyfin.Plugin.ELearning/Web/ThirdParty/`, license next to it), and its icons
are Material Icons paths (Apache-2.0).
