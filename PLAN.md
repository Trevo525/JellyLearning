# Jellyfin E-Learning Plugin — Plan

Goal: watch exported e-learning courses (Udemy, Skillshare, Dometrain, Craftsy, …) in Jellyfin
with a "native course site" feel: ordered sections/lectures, accurate resume tracking, course
progress, and downloadable/viewable resources (PDF, ZIP, XLSX, HTML, MD, …).

## Decisions (from interview, 2026-10-21)

| Topic | Decision |
|---|---|
| Server | Dedicated Jellyfin **12.2** instance, `lscr.io/linuxserver/jellyfin` (Docker). Pin the image tag (`version-12.2ubu2604`): `:latest` is already 12.4 and plugin packages must match the server |
| Clients | Web browser (jellyfin-web) only |
| Users | Multiple users; all progress is per-user |
| Library type | **Shows** library: Course = Series, Section = Season, Lecture = Episode |
| Ordering | Numeric prefixes, natural sort, alphabetical fallback |
| Completion | Lecture completes only near the very end (default: last 10 s **or** ≥ 98 %, configurable); otherwise exact resume position is kept |
| UI | Injected into jellyfin-web via the **File Transformation** plugin (dependency) |
| Attachments | Lecture-level, section-level, and course-level; inline view for PDF/HTML/MD/TXT/images, download for everything |
| Non-video lectures | HTML, Markdown/text, PDF-as-lecture shown in the course outline |
| Auto-play | Next **video** lecture (non-video items are skipped; Jellyfin's native "play next episode") |
| Artwork | `poster/cover/folder.jpg/png` if present, else Jellyfin frame grab of first lecture |
| Quizzes | Exports contain self-contained **HTML quizzes** → shown as non-video lectures in a script-enabled sandboxed iframe; score tracking deferred |
| Repo / CI | `<forgejo-host>/trevo525/jellyfin-plugin-elearning`, Forgejo Actions builds release zip + `manifest.json` plugin repository |

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
   (`002 Introduction.html`, `005 First steps….html`). Some HTML lectures are **interactive
   quizzes** (self-contained JS: timer, live score).
6. **Udemy scene release, mkv, quiz sections, per-lecture resource folders** —
   `UDEMY.Ufibato.Usinavaki.Zimoruk.Ilab.2026.BOOKWARE-UpAbE/`, `UDEMY.Amuzepir.IvatETA.Mastery…2026.BOOKWARE-UpAbE/`
   `01-Evaluti_Up/2-Erenorugat_UbimubeD_Ukekadi.mkv` (underscore titles); `01-Setting_Up/2-Resources/Download.html`
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
- **Link stubs**: an HTML file that is only a `meta refresh` / single link → shown as an external
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
│   ├─ ProgressStore         per-user state for non-video lectures (JSON/SQLite)│
│   ├─ ElearningController   /ELearning/... REST API (auth required)           │
│   └─ Web bundle            JS/CSS registered with File Transformation        │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Progress tracking (the "credits problem")

Jellyfin marks an item played once position passes `MaxResumePct` (server-wide, default 90 %)
and **resets the position to 0**. Plan:

1. On the dedicated instance set Dashboard → Playback → Resume → *Max resume percentage* = 99 or 100
   (documented in README; the plugin can optionally do this on first run).
2. `ProgressGuard` subscribes to `ISessionManager.PlaybackStopped` for items in course libraries:
   - stop position ≥ runtime − threshold → mark played.
   - otherwise → force `Played = false`, restore `PlaybackPositionTicks` from the event.
3. Unit tests for the threshold logic. Integration check: stopping at 92 % of a lecture keeps
   the resume point.

### API (`/ELearning`, all `[Authorize]`)

- `GET Courses/{seriesId}/Outline` → sections → items (video lectures with Jellyfin item IDs,
  non-video lectures, attachments), plus per-user completion counts.
- `GET Files/{fileId}` → stream file; `?download=true` → `Content-Disposition: attachment`.
  Files are addressed **only by index ID**, never by raw path (prevents path traversal).
- `GET Files/{fileId}/Render` → Markdown → HTML (Markdig); HTML served with a strict CSP and
  shown in a sandboxed iframe.
- `POST Items/{itemId}/Complete` / `DELETE …` → non-video lecture completion per user.

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
- [x] Standalone git repo (dotfiles ignores it via its `*` rule). Remote on Forgejo pending.
- [x] Scaffold from `jellyfin/jellyfin-plugin-template` → `Jellyfin.Plugin.ELearning`,
  `net10.0`, `Jellyfin.Controller`/`Jellyfin.Model` **12.2.0**, targetAbi `12.2.0.0`. Loads as Active.
- [x] `dev/docker-compose.yml`: 12.2 instance (pinned) + File Transformation 3.0.3.0 (verified 12.2
  build), real sources mounted read-only at `/data/elearning`; `dev/deploy.sh` builds + installs.
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
  cut to "4 Types Of"; `9 Vavel Ogebunobo Pt 2` → E2; `001 X` inside section folders → **S0
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
  real sources, Skillshare/Craftsy from the interview). **Snapshot tests** pin the full outline of
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
- Known limitation: slugs lose apostrophes ("Lazi S Refeku For Learning"); fix via the later
  `course.json` override if it matters.
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
- [x] `FirstLectureImageProvider` (`IDynamicImageProvider`, "E-Learning first lecture"): course
  and section artwork = frame of the first video lecture, unless a `poster/cover/folder` image
  exists. **Must be enabled per library** (Shows + Seasons image fetchers).
  `CourseArtworkTask` (post-scan) fills images that were missing on a course's first scan
  (sections are refreshed before their videos are probed).
- [x] `dev/verify-library.py`: compares jellyfin-web's view with the course index for every
  course; passes for 13 series / 825 lectures (real sources + fixtures) including subtitles and
  artwork, also on a from-scratch first scan. A non-course library on the same instance keeps
  plain-12.2 behaviour (checked).
### Phase 4 — ProgressGuard
- Completion threshold config (seconds + percent, per course library).
- Tests + manual verification on fixtures.

### Phase 5 — API
- Controller endpoints above, auth, safe file serving, Markdown rendering.

### Phase 6 — Web UI injection
- Register bundle with File Transformation; course/section/lecture panels, outline, viewer,
  progress bar. Confirm native "play next episode" handles auto-play across sections.

### Phase 7 — Packaging & distribution
- Forgejo Actions: build, test, zip, generate/update `manifest.json`, publish release.
- Add repo URL in Jellyfin → install/update from there. README with install + server settings.

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
- **12.2 extension APIs**: resolver/provider interfaces verified in Phase 1 (v12.2 source +
  running spike). Still to verify: `ISessionManager.PlaybackStopped` args (Phase 4).
- **Server upgrades**: plugin is built per server version; bump image tag, packages and
  `targetAbi` together, and re-run `dev/ui-view.py` against the real sources to catch
  resolver/provider behaviour changes.
