# JellyLearning — Plan

Goal: watch exported e-learning courses (Udemy, Skillshare, Dometrain, Craftsy, …) in Jellyfin
with a "native course site" feel: ordered sections/lectures, accurate resume tracking, course
progress, and downloadable/viewable resources (PDF, ZIP, XLSX, HTML, MD, …).

## Status (2026-11-08) — resume here

- **Done:** Phases 0–7, the real-library review, and Phase 8: pushed to GitHub, CI green, released
  `v1.0.0` (zip packaged with an `E-Learning_` name, upload glob bug fixed in the workflow),
  `v1.0.1` (everything renamed JellyLearning), `v1.1.0` (library type, see below), `v1.1.1` (plugin repository, see below). The repo is public and
  licensed GPLv3 (`LICENSE`, README "License").
- **Library type (v1.1.0):** Jellyfin's library type list is a fixed enum and jellyfin-web hard-codes
  the dialog, so there is no real "E-Learning" type. `elearning.js` adds a second Shows option,
  "JellyLearning courses", to the New Library dialog (`web.libraryCreator` selectors); saving with it
  creates the library with the course options and adds it to `CourseLibraryIds`
  (wraps `ApiClient.addVirtualFolder`). Verified in the dev instance's dialog; the web DOM can break it,
  the manual route (Shows library + tick in settings) always works.
- **Plugin repository (v1.1.1):** `manifest.json` on `main`
  (`https://raw.githubusercontent.com/Trevo525/JellyLearning/main/manifest.json`) lists JellyLearning
  and File Transformation 3.0.3.0 (12.2 build, copied from the author's manifest, checksum included).
  Jellyfin needs the plugin files at the **root** of the zip and an **MD5** checksum, so
  `build/package.sh` zips flat, and the release job runs `build/update-manifest.py` and commits the
  new entry to `main` (that commit exists only on GitHub: pull before pushing). Verified from the real
  public URL on a brand-new Jellyfin 12.2: both plugins installed through the catalog and loaded
  Active. Re-copy the File Transformation entry into `manifest.json` when it updates.
- **Naming:** the plugin is JellyLearning everywhere visible (zip `JellyLearning_<version>.zip`,
  plugin folder, plugin list, fetcher labels, console logs). Internal names (assembly
  `Jellyfin.Plugin.ELearning`, `/ELearning` API route, namespaces) keep the working title on purpose.
- **Next:** "Later" below. Still to do by hand: delete the pre-rewrite backups (see Phase 8 list).
- **Repo:** GitHub `Trevo525/JellyLearning` (user decision, 2026-11-08); CI is GitHub Actions
  (the workflow only runs on github.com). The history was rewritten twice on 2026-11-08 (real
  course names; then identity and leftovers), so it is published from this rewritten clone.
- **Dev loop:** `docker compose -f dev/compose.yaml up -d` → http://localhost:8096, built and set
  up automatically (plugin, fixtures, wizard, File Transformation, libraries, API key; see
  dev/README.md); `./dev/deploy.sh` (rebuild + install + restart); `dotnet test`; then
  `dev/verify-library.py`, `dev/test-progress.py` and `dev/test-api.py` (they read `dev/.env`
  themselves; test-api also creates the no-access user `learner-outsider`).
  `dev/.env` (gitignored, written by `dev/setup.py`) holds `JELLYFIN_API_KEY`, the admin login
  and the test user `JELLYFIN_TEST_USER` / `JELLYFIN_TEST_PASSWORD`; on this machine also
  `ELEARNING_SOURCE` (the real exports). Libraries: "Courses" (`/data/elearning`, real exports,
  read-only, only when `ELEARNING_SOURCE` is set) and "Course Fixtures" (`/data/courses`,
  from `dev/make-fixtures.sh`), both ticked in the plugin settings.
- **Reference source:** 12.2 server code is at tag `v12.2` of github.com/jellyfin/jellyfin
  (sparse clone of `MediaBrowser.*`, `Emby.*` was enough to answer every extension question).

## Decisions (from interview, 2026-10-21)

| Topic | Decision |
|---|---|
| Server | Dedicated Jellyfin **12.2** instance, `lscr.io/linuxserver/jellyfin` (Docker). Pin the image tag (`version-12.2ubu2604`): `:latest` is already 12.4 and plugin packages must match the server |
| Clients | Web browser (jellyfin-web) only |
| Users | Multiple users; all progress is per-user |
| Library type | **Shows** library: Course = Series, Section = Season, Lecture = Episode |
| Ordering | Numeric prefixes, natural sort, alphabetical fallback |
| Completion | Lecture completes only near the very end (default: last 10 s **or** ≥ 98 %, configurable); otherwise exact resume position is kept (< 5 s → start). Completed lectures stay completed when rewatched |
| UI | Injected into jellyfin-web via the **File Transformation** plugin (dependency) |
| Attachments | Lecture-level, section-level, and course-level; inline view for PDF/HTML/MD/TXT/images, download for everything |
| Non-video lectures | HTML, Markdown/text, PDF-as-lecture shown in the course outline |
| Auto-play | Next **video** lecture (non-video items are skipped; Jellyfin's native "play next episode") |
| Artwork | `poster/cover/folder.jpg/png` if present, else a generated cover (title on a purple/blue gradient) |
| Quizzes | Exports contain self-contained **HTML quizzes** → shown as non-video lectures in a script-enabled sandboxed iframe; score tracking deferred |
| Repo / CI | GitHub; GitHub Actions tests every push and attaches the release zip to tagged releases (no plugin repository, see Phase 7) |

## Observed export layouts (parser test fixtures)

1. **Flat, inconsistent prefixes** — `3D Kozenopi - A Amuzepir Course with Nebedo Babofap/`
   `1 Introduction.mp4` … `10 Ninib Okarez Pt1.mp4`, `" 24 Peru Opalo 1.mp4"` (leading space),
   `18 Are Efipofuve Rukoledu ubotu the epog..mp4`, loose `Lusu Pabi AVUGIM - Nebedo.xlsx`,
   `egubak Lusu Pabi .pdf.pdf` (double extension).
2. **Flat, slug titles + resources folder** — `Skillshare - How to Tivav - …/`
   `01 - lazi-s-refeku-for-learning.mp4`, `Course Resources/*.pdf`.
3. **Sectioned, globally numbered lectures** — `Dometrain - Getting Started - Onabaru in .MOM/`
   `02. Onabaru/04. What is onabaru.mp4` (lecture numbers continue across sections), root `code.zip`.
4. **Scene-release folder name, mkv** — `Craftsy.-.Idizel.Teta.Lune.Erito.And.Ovobiz.2025.BOOKWARE-ZEKAKISE/`
   `001-sukuz_two-funi_erito.mkv`, `Guidebook/000-course_guidebook_….pdf`.

5. **Udemy export, sectioned, HTML lectures + `_en` subtitles** — `ivateta-konegore-mipunavel-kilulani-kafupap/`
   (lowercase slug course name) `01 - Introduction to Course/003 Introduction.mp4`,
   `003 Introduction_en.srt`/`.vtt`, and HTML lectures interleaved by number
   (`002 Introduction.html`, `005 First uvala….html`). Some HTML lectures are **interactive
   quizzes** (self-contained JS: timer, live score).
6. **Udemy scene release, mkv, quiz sections, per-lecture resource folders** —
   `UDEMY.Ufibato.Usinavaki.Zimoruk.Ilab.2026.BOOKWARE-UpAbE/`, `UDEMY.Amuzepir.IvatETA.Mastery…2026.BOOKWARE-UpAbE/`
   `01-Evaluti_Up/2-Erenorugat_UbimubeD_Ukekadi.mkv` (underscore titles); `01-Evaluti_Up/2-Resources/Umuvamor.html`
   (resources for lecture 2; many are `<meta http-equiv="refresh">` **link stubs**); `69-Resources/*.Kpart|Xmod|pdf|jpg`;
   **quiz-only sections** `02-QUIZ_1_IvatETA_Emememezigif/7-Asakuvupivu_meninebon_-_Quiz.html` (no video);
   release junk at the course root: `*.rar`, `*.r00`–`*.r48`, `*.sfv`, `*.nfo`.

### Parsing rules derived from these

- **Prefix regex**: `^\s*(\d+)\s*(?:[.\-_)]\s*|\s+)` → order number; remainder → title.
- **Natural sort** on the parsed number, then on the title.
- **Section detection**: a subfolder containing ≥1 video = section (Season). A course with no
  such folders gets one implicit section ("Lectures", season 1).
- **Resource folders**: a subfolder with no videos (e.g. `Course Resources/`, `Guidebook/`) =
  course-level resources (or section-level if nested inside a section).
- **Lecture-level attachments**: non-video file whose cleaned base name matches a lecture's
  base name or prefix number (e.g. `03 Setup.mp4` + `03 Setup - slides.pdf`).
- **Episode index** = position within its section (not the raw number, since Dometrain numbers
  are course-global). Raw number kept for display ("Lecture 04").
- **Title humanizing**: if a title contains no spaces, replace `_`/`-` with spaces and
  title-case; otherwise keep as-is (preserves "Free-Form"). Strip trailing dots, collapse spaces.
- **Course folder name**: dots → spaces when there are no spaces; strip scene suffix
  (`.YEAR.TAG-GROUP`); pull out `YEAR` → ProductionYear; leading `Provider - ` → Studio + tag.
- **Doubled extensions** (`.pdf.pdf`) normalized for display only; files are never renamed.
- Video extensions: mp4, mkv, webm, m4v, mov, avi. Subtitles: sidecar `.srt/.vtt/.ass` with the
  same base name. Jellyfin handles `X.en.srt` natively, but the Udemy-style **`X_en.srt` is not
  detected at all** (Phase 1), so the plugin must surface those itself.
- *(from layouts 5–6)* **Section detection, revised**: a numbered subfolder containing ≥1 video
  **or ≥1 numbered lecture document** (`.html/.md/.pdf`) is a section. So `02-QUIZ_1_…` is a section
  with one non-video lecture, not a resource folder. Quiz-only sections have no Jellyfin Season
  (no episodes) and exist only in the plugin's outline.
- **Lecture resource folders**: `N-Resources/` (also `N - Resources`, `Resources N`) inside a section
  → attachments of lecture N in that section.
- **Numbered documents interleaved with videos** (`002 Introduction.html` between `001`/`003`
  videos) are **non-video lectures** in the outline, not attachments. An attachment needs a name
  match to a lecture (same number + similar title, or `… - slides` suffix).
- **Link stubs**: an HTML file that only `meta refresh`es to an external URL → shown as an external
  link (open in new tab) instead of an inline viewer.
- **HTML quizzes** need JS → sandboxed iframe with `allow-scripts` but **without**
  `allow-same-origin`, served from the plugin with a CSP. Quiz score tracking stays deferred;
  "completed" is set manually or on open.
- **Release junk ignored**: `*.rar`, `*.r[0-9][0-9]`, `*.sfv`, `*.nfo` (and `*.par2`) are hidden
  everywhere; the `.nfo` is never used as course metadata.
- **Course name**: `UDEMY.` / `SKILLSHARE.` etc. leading scene token → Studio. Lowercase slug
  names (`ivateta-konegore-mipunavel-kilulani-kafupap`) → humanized title.

## Architecture

```
┌─────────────────────────── Jellyfin 12.2 server ────────────────────────────┐
│  Shows library (marked as "course library" in plugin config)                │
│                                                                              │
│  ELearning plugin (.NET 10, Jellyfin.Controller 12.2.x)                     │
│   ├─ CourseParser          pure, unit-tested folder → Course model           │
│   ├─ Season resolver     IItemResolver: video folder under a course = Season │
│   ├─ Custom providers    ICustomMetadataProvider: names, index numbers,      │
│   │                         studio, year, artwork                             │
│   ├─ CourseIndex           cached outline incl. non-video items + attachments│
│   │                         rebuilt on library scan (ILibraryPostScanTask)    │
│   ├─ ProgressGuard         PlaybackStopped hook → enforce completion rule    │
│   ├─ ProgressStore         per-user state for non-video lectures (JSON)      │
│   ├─ ElearningController   /ELearning/... REST API (auth required)           │
│   └─ Web bundle            JS/CSS registered with File Transformation        │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Progress tracking (the "credits problem")

Jellyfin marks an item played once position passes `MaxResumePct` (server-wide, default 90 %)
and **resets the position to 0**. Plan:

Implemented in Phase 4 (see there): `ProgressGuard` re-applies the course rule after
Jellyfin's own on every progress report and stop, for items in course libraries only.

### API (`/ELearning`, all `[Authorize]`)

- `GET Courses/{itemId}/Outline` (series, season or episode ID) → sections → lectures (videos
  with Jellyfin item IDs, non-video lectures, attachments), plus per-user completion counts.
- `GET Files/{fileId}` → stream file; `?download=true` → `Content-Disposition: attachment`.
  Files are addressed **only by index ID**, never by raw path (prevents path traversal).
- `GET Files/{fileId}/Render` → Markdown → HTML (Markdig); HTML served with a strict CSP and
  shown in a sandboxed iframe.
- `POST Lectures/{lectureId}/Complete` / `DELETE …` → non-video lecture completion per user.
- All take an optional `userId` (API keys and administrators only, like Jellyfin's endpoints).

### Web UI (injected)

- **Course page (Series)**: progress bar "34 / 120 lectures • 28 %", Resources panel
  (course-level files), full outline including non-video lectures.
- **Section page (Season)**: section resources + section progress.
- **Lecture page (Episode)**: lecture attachments.
- **Viewer modal**: PDF (browser viewer in iframe), HTML (sandboxed iframe), MD/TXT (rendered),
  images; download button on every file.
- Hooks jellyfin-web route changes; must degrade gracefully (no errors) if selectors change.

## Phases

### Phase 0 — Setup
- [x] Standalone git repo (dotfiles ignores it via its `*` rule).
- [x] Scaffold from `jellyfin/jellyfin-plugin-template` → `Jellyfin.Plugin.ELearning`,
  `net10.0`, `Jellyfin.Controller`/`Jellyfin.Model` **12.2.0**, targetAbi `12.2.0.0`. Loads as Active.
- [x] `dev/docker-compose.yml`: 12.2 instance (pinned) + File Transformation 3.0.3.0 (verified 12.2
  build), real sources mounted read-only at `/data/elearning`; `dev/deploy.sh` builds + installs.
  Replaced on 2026-11-08 (user request: a clone should run the dev scripts easily) by
  `dev/compose.yaml`, which also builds the plugin (`plugin` service, `dev/build-plugin.sh` in
  the dotnet SDK image, on a copy of the sources), generates the fixtures (`fixtures` service,
  `make-fixtures.sh --if-changed`, ffmpeg from the Jellyfin image) and sets the server up
  (`setup` service, `dev/setup.py`: wizard, File Transformation + restart, libraries without
  internet metadata, plugin settings, API key → `dev/.env`; only adds what is missing). Prep
  services run as root but hand what they write to PUID:PGID; `dev/devenv.py` loads `dev/.env`
  for the scripts. Verified from an empty state folder: set up in ~30 s, test-api 34/34,
  test-progress passes, verify-library OK; a second `up` changes nothing.
- [x] `dev/make-fixtures.sh`: rebuilds all fixture layouts as tiny courses (30 s ffmpeg clips,
  placeholder docs) in `dev/jellyfin/courses/` = `/data/courses` (done in Phase 2).

### Phase 1 — Spike: library mapping (the biggest risk) — DONE
- Point a Shows library at the fixtures **without** the plugin; record how 12.2 mis-parses them
  (seasons from `02. Onabaru`, episode numbers from `04.`, flat folders, resource folders).
- Prove the chosen mechanism (resolver vs. metadata provider vs. both) produces correct
  Series/Season/Episode numbering and order for all 4 fixtures. Pick the approach before Phase 2.

#### Phase 1 results (2026-11-02, real sources, 405 videos in 5 courses)

Baseline — plain 12.2, internet metadata off (`dev/ui-view.py` = what jellyfin-web lists):
- **Section folders never get a season number** (`S?`), raw names (`01. Course anorebag`).
- **Episode numbers are guessed from titles**: `4 Arape Of 3D Kozenopi` → E3 *and* the title is
  cut to "4 Arape Of"; `9 Vavel Ogebunobo Pt 2` → E2; `001 X` inside section folders → **S0
  "Specials"**, so episodes show twice (Specials + their folder); `100-Foo.mkv` → S1E0.
- **Flat course** → "Season Unknown", alphabetical order (1, 10, 12, …, 2, 20 …).
- **`15-3D_Simarabo…` is not a season**: 12.2's SeasonResolver rejects folder names that parse
  as an episode code (S15E3), so its lectures land loose in the series.
- Course names: `Onabaru in .MOM` → "Onabaru in  MOM"; scene names kept verbatim; no studio.
  Year from scene names (2026) works.
- **`X_en.srt` / `X_en.vtt` sidecars are not detected at all** (0 external subtitle streams).
- `N-Resources/` folders become plain Folder items (hidden from the show UI); docs ignored.

**Mechanism chosen** (proven by the spike in `Jellyfin.Plugin.ELearning/Spike/`):
1. **`IItemResolver`, priority `Plugin`**: a direct subfolder of a course that contains videos
   is always a `Season`. Needed to override the episode-code rejection above.
2. **`ICustomMetadataProvider<Series|Season|Episode>`** set `Name`, `IndexNumber`,
   `ParentIndexNumber`. Custom providers run after the local/remote merge and write straight to
   the item. *Local* providers can't be used: `MetadataService` pre-seeds
   `ParentIndexNumber` from the item and merges local results with `replaceData = false`.
3. Season number = position among video-containing section folders (contiguous, so quiz-only
   sections don't leave gaps); episode number = position within the section; flat courses use
   Jellyfin's auto-created path-less season 1 (to be named "Lectures").

Spike result: all 405 videos in exactly one correct season, in course order, clean titles,
no Specials/duplicate seasons. Jellyfin's own `RemoveObsoleteSeasons` cleaned up the
baseline's virtual seasons, and a normal library scan keeps the values.

Carry into Phases 2–3:
- Scope the resolver/providers to configured course libraries — done in Phase 3.
- **Numbering must be stable**: episode user-data keys include series key + S/E numbers, so
  renumbering after users have progress may orphan their resume points. Freeze the rules
  before real use; add a test that pins numbering for every fixture.
- Subtitles: plugin must surface `_<lang>` sidecars — done in Phase 3 (copy into the internal
  metadata folder under Jellyfin's naming).
- Verify "play next episode" crosses section boundaries (Phase 6 check, numbering looks right).

### Phase 2 — CourseParser + CourseIndex — DONE
- [x] Pure parser (`Courses/CourseParser.cs`, `CourseNaming.cs`) working on relative paths only.
- [x] Fixtures = file listings of the 7 layouts (`tests/.../Fixtures/Listings/`, 5 exported from the
  real sources, Skillshare/Craftsy from the interview; new fixtures use invented names, see
  "Real-library review"). **Snapshot tests** pin the full outline of
  each (`Fixtures/Snapshots/`) — a snapshot diff means numbering changed; review before accepting
  (`UPDATE_SNAPSHOTS=1 dotnet test`). Plus per-rule unit tests (76 tests total).
- [x] `CourseIndex`: persisted at `<DataPath>/elearning/course-index.json` (dev host:
  `dev/jellyfin/config/data/data/elearning/`), rebuilt by `CourseIndexTask` (`ILibraryPostScanTask`)
  for the libraries ticked in the plugin settings. Files resolve **by ID only** (SHA-256 of course
  folder + relative path, 24 hex chars). Dev check: 5 courses / 534 lectures indexed.

Rules settled while implementing (in addition to "Parsing rules" above):
- A document-only folder is a section only if the **folder is numbered** and holds numbered
  documents; so `Guidebook/000-guide.pdf` stays a course resource, `02-QUIZ_1_…/7-….html` is a section.
- Numbered non-video file with the same number as a video: **attachment** if it is not a lecture
  type (zip, xlsx, Kpart, …) or its title extends the video's (`03 Setup - slides.pdf`);
  otherwise it is its own lecture (Udemy `002 Isokatotola.html` after `001 … .mp4`).
- Lecture order within a section: number, then videos before documents, then natural title.
- Loose videos next to section folders form an implicit first section "Lectures".
- Subtitles: `<video>{.|_|-| }<lang>[.<flag>].<ext>`, matched to the longest video name;
  unmatched subtitles become section resources.
- Cover: `poster|cover|folder.<image>` in the course folder.
- ~~Known limitation: slugs lose apostrophes~~ — fixed 2026-11-08 (contraction rule, see
  "Real-library review").
- **For Phase 3**: library scans run metadata providers *before* post-scan tasks, so providers
  must parse the course themselves (`CourseScanner.Scan`, cached per scan), not read the index.

### Phase 3 — Metadata — DONE
All in `Jellyfin.Plugin.ELearning/Library/`, everything scoped to the libraries ticked in the
plugin settings (`CourseLibraries`); courses are parsed on demand by `CourseCatalog` (30 s cache)
because providers run before the post-scan index rebuild. The Phase 1 spike is removed.
- [x] `SectionResolver` (`IItemResolver`, priority Plugin): video section folder → Season.
- [x] `CourseIgnoreRule` (`IResolverIgnoreRule`): Jellyfin only sees video section folders and
  video lectures; resource folders, `N-Resources/`, quiz-only sections and stray videos are hidden
  (served later by the plugin API).
- [x] `CourseProvider` / `SectionProvider` / `LectureProvider` (`ICustomMetadataProvider`): course
  title, studio, year; section number + title (flat courses: "Lectures"); lecture S/E numbers +
  title. Fields the user locked in the metadata editor are left alone.
- [x] `SubtitleSidecarProvider` (pre-refresh, order 10 < probe's 100): copies `X_en.srt` into the
  item's internal metadata folder as `X.en.srt`, where 12.2's external-subtitle scan looks too.
  Course folders are never written to. Result: "English - SUBRIP/WEBVTT - External" tracks.
- [x] ~~`FirstLectureImageProvider`~~ (frame of the first lecture) — replaced after Phase 7 by
  `CourseCoverProvider`: generated covers for courses and sections (`CoverRenderer`, SkiaSharp
  from the server: blurred purple/blue gradient, title, studio / course name). An
  `ILocalImageProvider` (order 1), so it is always on, applies to existing items on any scan,
  and loses to a `poster/cover/folder` image in the course folder (Jellyfin's own local images,
  order 0). Covers are cached in `<data>/elearning/covers/`, keyed by text + `DesignVersion`.
- [x] `dev/verify-library.py`: compares jellyfin-web's view with the course index for every
  course; passes for 13 series / 825 lectures (real sources + fixtures) including subtitles and
  artwork, also on a from-scratch first scan. A non-course library on the same instance keeps
  plain-12.2 behaviour (checked).
### Phase 4 — ProgressGuard — DONE
- [x] `Progress/CompletionRule.cs` (pure, unit-tested): complete when stopped within
  `CompletionSecondsFromEnd` (10) of the end **or** at `CompletionPercent` (98) — position then
  resets to 0; otherwise the exact position is kept (positions < 5 s → 0). A lecture that was
  already complete stays complete when rewatched, but its resume position is remembered.
- [x] `Progress/ProgressGuard.cs` (hosted service): Jellyfin applies its movie rules and saves
  *first*, then raises the playback events, so the guard re-saves the corrected state on every
  progress report and on stop. Played-before is snapshotted from the synchronous
  `UserDataSaved(PlaybackStart)` — `ISessionManager.PlaybackStart` is raised asynchronously and
  can arrive after the first progress report.
- Found in 12.2's `UserDataManager.UpdatePlayState`: besides `MaxResumePct`, **`MinResumeDurationSeconds`
  (300) marks every video shorter than 5 minutes as played once 5 % are watched**, and
  `MinResumePct` (5) drops early positions. The guard overrides all three, so the server
  settings no longer matter for course libraries (MaxResumePct = 100 only avoids a brief
  "watched" flicker for other clients while a lecture plays).
- [x] `dev/test-progress.py`: plays lectures as a test user via the web client's session
  endpoints; 6/6 scenarios pass with Jellyfin's default MaxResumePct = 90.
- Thresholds are global (plugin settings), not per library — enough for one dedicated instance.
- Manually confirmed in the browser (user, 2026-11-02): half-watched lecture resumes and stays
  unwatched; 12 s before the end stays in progress; 4 s before the end marks it complete.

### Phase 5 — API — DONE
- [x] `Api/ELearningController.cs` (`/ELearning`, endpoints in "API" above), all `[Authorize]`.
  The course is found from the series path (`CourseIndex.GetCourse`), files only by ID
  (`CourseIndex.GetFile`). Every endpoint checks that the course is still in a ticked library
  and that the user may see its series (`IsVisibleStandalone`: library access, parental rating);
  unknown, inaccessible and missing all answer 404.
- [x] **Outline** (`Api/OutlineBuilder.cs`, pure): lectures with episode IDs, played state,
  position and runtime for videos; `ProgressStore` completion for documents; section/course
  totals (`Percent` rounds down, so 100 means all done); file sizes; `ExternalUrl` on link stubs.
  Quiz-only sections have no `SeasonId`.
- [x] **Files**: `PhysicalFile` with range support, `download=true` → attachment with the
  display name, `nosniff`, and a no-script sandbox CSP on everything except PDFs (browsers
  refuse to show PDFs in sandboxed documents).
- [x] **Render**: Markdown → HTML (Markdig 1.5.1, `DisableHtml`), text → `<pre>`, HTML lectures
  as they are with `Documents.ScriptPolicy` (sandboxed scripts, no same-origin, no network, no
  forms/frames), link stubs → a link page. Real quizzes only use inline `onclick` handlers and
  external images, no forms, storage or relative assets (checked all 32 with scripts).
- [x] **Link stubs** (`Api/LinkStub.cs`): `meta refresh` to an absolute http(s) URL, files ≤ 32 KB.
  All 120 stubs in the real exports are this shape, so the "single link page" heuristic was
  dropped (it would misfire on short HTML lectures). Cached per file by mtime/size.
- [x] **Non-video completion**: `POST`/`DELETE Lectures/{lectureId}/Complete` (renamed from
  `Items/…`: these are plugin lecture IDs, not Jellyfin item IDs) → `Progress/ProgressStore.cs`,
  one JSON file per user in `<data>/elearning/progress/`; unreadable files are moved aside.
- **ID scheme fixed**: file IDs were hashed from the course *folder name*, so the same course in
  two libraries (as on the dev server) shared IDs and `Files/{id}` served the wrong copy. File
  IDs are now hashed from the absolute course path (`CourseParser.Parse(..., idScope)`);
  document progress uses `ProgressStore.Key` = folder name + relative path, so it survives
  moving the library. Neither appears in snapshots, so season/episode numbering is unchanged.
- [x] Tests: 123 unit tests (link stubs, rendering escapes, CSPs, progress store, outline against
  the fixture courses, tampered index paths outside the course, unknown IDs, ID collisions).
  `dev/test-api.py`: 28/28 live checks, including range bytes vs. disk, headers, a user
  without library access, and no token. `verify-library.py` and `test-progress.py` still pass.
- `Markdig.dll` ships next to the plugin dll (`build.yaml` artifacts, `dev/deploy.sh`).
- Fixed in passing: `dev/make-fixtures.sh` deleted and recreated `dev/jellyfin/courses`, which
  detaches the running container's bind mount (it then sees an empty folder); it now empties
  the folder in place. New fixture `Sample - Markdown Notes` covers Markdown/text lectures.

#### Rules for the UI (Phase 6)
- **Never put the token in a URL that a document can see.** Load HTML lectures and Markdown/text
  by `fetch` with the `Authorization` header from `Files/{id}/Render` and show them with
  `<iframe sandbox="allow-scripts allow-popups allow-popups-to-escape-sandbox" srcdoc=…>`
  (no `allow-same-origin`). `Render` refuses `api_key`/`ApiKey` in the query.
- PDFs and images: `fetch` → blob URL in an iframe/img. Downloads: `Files/{id}?download=true&api_key=…`
  is fine (an attachment is never rendered), as jellyfin-web does for its own downloads.
- Link stubs (`ExternalUrl` set): open in a new tab with `rel="noopener noreferrer"`.
- Jellyfin's JSON omits null properties (`ItemId`, `SeasonId`, `ExternalUrl`, `Size`, …).
- Mark document lectures complete via `Lectures/{id}/Complete` (on open, plus a toggle);
  videos complete through playback only (400 otherwise).

### Phase 6 — Web UI injection — DONE
- [x] `Web/WebInjection.cs` (hosted service) registers an `index.html` transformation with File
  Transformation 3.0.3.0 by reflection (`PluginInterface.RegisterTransformation(JObject)`, the
  JObject built via the parameter's own type, callback = static `WebInjection.TransformIndex`).
  Without File Transformation it logs a warning and the rest of the plugin still works.
- [x] The callback adds `<script defer src="../ELearning/Web/elearning.js?v=<hash>">` before
  `</body>`; the script is an embedded resource served anonymously and cached as immutable
  (the content hash changes with every build).
- [x] `Web/elearning.js` (vanilla JS, no build step): on every `viewshow` of `#itemDetailPage`
  (the event bubbles; 12.2's details page is still the legacy controller) it fetches the
  outline for the item in the URL; 404 = not a course → nothing happens (remembered per item).
  All jellyfin-web specifics are in its `web` object (`window.ApiClient`, the page id, the mount
  point `.detailPageSecondaryContainer`, `#/details?id=` links).
  - **Course page**: progress bar, Continue/Start button (resume position first, else first
    open lecture), course resources, collapsible outline (section with the next lecture open)
    including quiz-only sections and document lectures, with done/resume state.
  - **Section page**: section progress, resources, all lectures with their attachments.
  - **Lecture page**: breadcrumb + position, lecture resources, the next 3 lectures in the
    course (so readings/quizzes between videos aren't missed when auto-play skips them).
  - **Viewer** (full-screen overlay, Esc closes): HTML/Markdown/text via `Render` + `srcdoc` in a
    sandboxed iframe, PDF/images via blob URLs, download button; opening a document lecture
    marks it complete, with a toggle to undo. Link stubs open in a new tab.
- **Course player (added after the first browser test, at the user's request: "emulate Udemy")**:
  `Web/player.html|js|css`, served at `<base>/ELearning/Web/player.html#<seriesId>[/<lectureId>]`.
  Udemy layout: top bar (back to Jellyfin, course title, progress ring), video/document stage
  with a lecture bar (previous / title / next), Overview + Resources tabs, and a sticky "Course
  content" sidebar (numbered sections with "done / total | minutes", lecture checkboxes, kind +
  duration, "Resources" dropdowns, resume bars; stacks under the stage below 64rem).
  - Signs in with the session jellyfin-web stored in the same origin (`jellyfin_credentials`,
    matched by `/System/Info/Public` Id) and sends **only the token**, so Jellyfin takes client
    and device from the token's own device (checked in 12.2's `AuthorizationContext`).
  - Plays via `PlaybackInfo` with its own device profile: mp4/webm direct play, everything else
    (mkv) as HLS from Jellyfin (video stream-copied when h264) through bundled **hls.js 1.7.4**
    (light build, Apache-2.0, npm tarball integrity verified) or native HLS on Safari. Sidecar and
    embedded subtitles come as external WebVTT tracks. Resume from the user's position.
  - Reports `/Sessions/Playing`, `/Progress` (every 10 s, pause/unpause/seek) and `/Stopped`
    (on lecture change, end, and `pagehide` with `keepalive`), so ProgressGuard, resume and
    transcode cleanup work as with jellyfin-web. Outline reloaded 1.5 s after a stop.
  - Auto-advance: 5 s "Up next" countdown to the next lecture of any kind (cancelable).
  - Checkboxes: videos via `/UserPlayedItems`, documents via `Lectures/{id}/Complete`.
  - jellyfin-web panels now link into it: course page "Start/Continue course", every lecture
    title, and "Open in course player" on lecture pages. Panels moved into the content column
    above "Next Up"; lecture resources folded behind a paperclip toggle.
- Native auto-play: jellyfin-web's `playbackManager.getEpisodes` queues up to 100 episodes of
  the *series* from the started one, so it continues across sections (to confirm in the browser).
- Rendered Markdown/text pages are light-only (like the exports' HTML lectures) so they read the
  same on Jellyfin's dark theme.
- Tests: `WebAssetsTests` (injection once, before `</body>`; callback payload; all files embedded;
  player page versioned; unknown names refused). `dev/test-api.py` 33/33: index.html carries the
  tag, files served, and the player's device profile gets direct play for mp4 and working HLS
  (playlist + first segment) for mkv.
- Confirmed in the browser by the user (2026-11-03): player playback (mp4 direct, mkv remuxed to
  HLS with stream copy, transcodes cleaned up on stop), resume, checkboxes, resources, subtitles,
  Up next. Quizzes in lecture resources: offered before moving on, marked "Quiz" in the
  sidebar, deliberately **not** tracked as done (user decision). Exported HTML keeps its own
  (light) styling; a dark reading mode was offered, not requested.

### Phase 7 — Packaging & distribution — DONE (workflow untested until the first GitHub run)
- **Plugin repository** (originally none, because the first host required sign-in; added on GitHub
  later, see Status): `manifest.json` in the repo root, served raw from `main`. Manual install stays
  available (README, "Install").
- [x] `build/package.sh [version]`: Release publish with the version stamped into the assembly,
  copies the `build.yaml` artifacts (plugin dll, Markdig.dll), writes `meta.json` from
  `build.yaml`, zips `E-Learning_<version>/` + a `.sha256`. Verified on the dev server: unzipped
  0.1.0.0 loads as Active and `dev/test-api.py` passes 33/33.
- [x] `.github/workflows/build.yml` (GitHub Actions since 2026-11-08, user decision):
  tests on every push/PR; on `v*` tags also packages and `gh release create` publishes the zip +
  `.sha256`, with notes from `build/release-notes.py` (build.yaml changelog + install steps).
- `dev/deploy.sh` now removes every installed `E-Learning_*` folder first (Jellyfin loads the
  highest version, so a leftover release would shadow dev builds).

### Real-library review (2026-11-08, after Phase 7)
The user imported a large real library (134 courses); findings and decisions:

- **Merged lectures (fixed)**: Jellyfin's folder-wide video resolver groups episodes whose path
  parses to the same SxxEyy into *versions* of one item — a folder name like "… 2024-6" made every
  file "S2024E6" — and it silently drops files with the word "sample" in their name.
  `LectureResolver` (`IMultiItemResolver`, priority Plugin) now resolves each lecture of a course
  folder on its own via `ILibraryManager.ResolvePath` (ends in Jellyfin's EpisodeResolver, which
  neither groups nor drops). Other libraries keep Jellyfin's behaviour. Already-grouped items
  split on the next scan with their IDs and watch data intact.
- **Stale names (fixed)**: our custom providers only run on an ordinary scan if they implement
  `IHasItemChangeMonitor` and report a change, so Course/Section/LectureProvider now compare
  the parsed values with the item (`Apply(item, write: false)`).
- **Naming rules added**: slug contractions (`Let_s` → "Let's", `don_t`, `we_ll`, `you_ve`, `_re`;
  needs a separator after it so "and_s" stays), dots as word gaps in dotted names when dots
  outnumber dashes (digit dots like "3.0" and dotfiles "..zshenv" → " .zshenv" stay; accepted
  side effect: "Step-By-Step.Clay" → "Step By Step.Clay"), `.part` files are junk.
- **Deliberately not changed** (user decisions):
  - Release tags (`-GROUP`) stay in titles; stripping them risks eating real title text.
  - No provider/studio guessing from nfo files or file names; folder name only.
  - A trailing "- <course title>" on lecture names is kept (could be part of a real title).
  - Numeric course IDs in some providers' names are kept.
  - Bad source folders (wrong or missing structure) are fixed by hand in the library, not by
    heuristics.
  - A year in the middle of a scene name (not at the end) isn't detected; acceptable.
  - Truncated source names (names cut to a few characters, e.g. `4___ABCD.MP4`) can't be repaired; the
    numbers set section/lecture order and are not shown in titles.
- **`.url` shortcuts** (user decision: as resources): `FileKind.Link`, no longer junk. A numbered
  one next to a lecture with the same number is an attachment, otherwise a section/course
  resource; `LinkStub.FromShortcut` reads the `URL=` line (http/https only) and the UI shows it
  as an external link, without the `.url` in its name.
- **Course words for Jellyfin's headings** (user decision): on course and section pages the
  injected script renames jellyfin-web's "Seasons" / "Episodes" headings to "Sections" /
  "Lectures" and "Next Up" to "Next lecture" (`web.headings` in `elearning.js`; a
  MutationObserver re-applies them, since jellyfin-web writes the heading after loading the
  children). Lecture pages and the rest of Jellyfin are unchanged.
- **Generated covers** (replace the first-lecture frame grab, see Phase 3). How they behave:
  - Course: title + studio; section: "SECTION N" + section title + course title; a flat
    course's one section: "LECTURES".
  - Cached in `<data>/elearning/covers/<sha256>.jpg`, keyed by the text + `DesignVersion`;
    a renamed course/section folder gets a new cover on the next normal scan. Bump
    `CoverRenderer.DesignVersion` to redraw all after a design change. First full render of
    694 covers took ~86 s of scan time.
  - Approved by the user as is (2026-11-08). Known rough edges, if ever needed: a line can end in a dangling " -"; relies on the
    server's DejaVu Sans (bundle a font for other hosts); an image uploaded through Jellyfin's
    UI is replaced on the next refresh (local images win), so overrides go in the course
    folder; old cache files are never deleted.
- **Test fixtures must not use the user's real course or file names** (user rule, 2026-11-08):
  fixtures, test cases, dev scripts and examples in code/docs use invented names with the same
  shape. The older fixtures were converted word by word (same length and case, parser keywords,
  providers and extensions kept), so their outlines stayed identical apart from attachment
  order; the git history was rewritten the same way (user decision). `dev/make-fixtures.sh`
  writes meta-refresh stubs for HTML in `N-Resources/` and example.com targets for `.url`
  files, so `dev/test-api.py` no longer needs a real course.

### Phase 8 — Publish on GitHub (last step for 1.0) — DONE
Nothing identifying the user may be in the repo or its history (user rule, 2026-11-08: no real
name, usernames, email or self-hosted server; course names already removed).
- [x] Public identity (user, 2026-11-08): author and committer `Trevor Vance`, GitHub user
  `Trevo525` (plugin `owner` in `build.yaml`, README links).
- [x] Set in this repo's git config (`git config user.name/user.email`).
- [x] History rewritten once more: author/committer on every commit, the commit message that
  named the old repo, the old repo URL and the library's path on disk in past versions, and
  leftovers of real names the first pass kept (a few file extensions, file names and lecture
  fragments, older test cases). Audit before publishing: every blob, path and commit message of
  all 25 commits against every file and folder name of the real library. No course names are
  left; what still matches is generic vocabulary ("Introduction", "About Me", "Course
  Resources", programming terms). `dotnet test` passes on the rewritten history.
- [x] GitHub repo created and added as `origin`.
- [x] Push `main`.
- [x] Check the first GitHub Actions run (`.github/workflows/build.yml`, untested so far).
- [x] Tagged `v1.0.0` (2026-11-08); first release job failed on the staging folder in the upload glob, fixed in `9d7ddaa`, tag moved, release published.
- [x] Release zip installs on a clean server (checked by the user, 2026-11-08).
- [ ] Afterwards: delete the pre-rewrite backup bundle and the word mapping from the
  2026-11-08 session scratchpad, if they still exist (they hold what the rewrite removed).

### Later
- Quiz score tracking (read results from the HTML quiz via postMessage).
- Optional per-course `course.json` override (titles/order/description).
- Course-level "Continue where you left off" button.

## Open risks
- ~~File Transformation compatibility with 12.2~~: resolved — 3.0.3.0 has a 12.2.0.0 build and
  loads in the dev instance. (Fallback if it ever lags a server upgrade: serve the bundle from the
  plugin and load it through a generic custom-JS plugin.)
- **jellyfin-web DOM changes** between releases can break injected UI. Keep selectors isolated
  in one module.
- **MKV in browser** may force transcoding; check the dedicated instance's hardware acceleration.
  The Screen Grabber also failed on a few Udemy `.mkv` files during the Phase 1 scan; check
  whether those files play and fall back to the course cover for their thumbnails.
- ~~12.2 extension APIs~~: resolver/provider/ignore-rule/image-provider interfaces (Phases 1, 3)
  and session/user-data events (Phase 4) verified against the v12.2 source and the running server.
- **Server upgrades**: plugin is built per server version; bump image tag, packages and
  `targetAbi` together, then run `dotnet test`, `dev/verify-library.py`, `dev/test-progress.py`
  and `dev/test-api.py` to catch resolver/provider/playback/API behaviour changes.
