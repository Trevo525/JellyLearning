# Jellyfin E-Learning Plugin — Plan

Goal: watch exported e-learning courses (Udemy, Skillshare, Dometrain, Craftsy, …) in Jellyfin
with a "native course site" feel: ordered sections/lectures, accurate resume tracking, course
progress, and downloadable/viewable resources (PDF, ZIP, XLSX, HTML, MD, …).

## Decisions (from interview, 2026-10-21)

| Topic | Decision |
|---|---|
| Server | Dedicated Jellyfin **12.2** instance, `lscr.io/linuxserver/jellyfin:latest` (Docker) |
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
| Quizzes | **Deferred** until a real sample file exists |
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
  same base name. Jellyfin handles these natively.

## Architecture

```
┌─────────────────────────── Jellyfin 12.2 server ────────────────────────────┐
│  Shows library (marked as "course library" in plugin config)                │
│                                                                              │
│  ELearning plugin (.NET 10, Jellyfin.Controller 12.2.x)                     │
│   ├─ CourseParser          pure, unit-tested folder → Course model           │
│   ├─ Resolver / Ignore rule  keep resource folders & docs out of Seasons     │
│   ├─ Metadata providers    Series/Season/Episode names, index numbers,       │
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
- New standalone git repo (this folder currently sits inside the dotfiles repo; add it to the
  dotfiles `.gitignore` or move it).
- Scaffold from `jellyfin/jellyfin-plugin-template`; rename to `Jellyfin.Plugin.ELearning`;
  target .NET 10 and Jellyfin 12.2 packages (verify exact versions on NuGet).
- `dev/docker-compose.yml`: throwaway 12.2 instance + File Transformation plugin, plugin output
  mounted into `/config/data/plugins`.
- `dev/make-fixtures.sh`: rebuild the 4 sample layouts above with tiny ffmpeg-generated videos
  and dummy docs.

### Phase 1 — Spike: library mapping (the biggest risk)
- Point a Shows library at the fixtures **without** the plugin; record how 12.2 mis-parses them
  (seasons from `02. Onabaru`, episode numbers from `04.`, flat folders, resource folders).
- Prove the chosen mechanism (resolver vs. metadata provider vs. both) produces correct
  Series/Season/Episode numbering and order for all 4 fixtures. Pick the approach before Phase 2.

### Phase 2 — CourseParser + CourseIndex
- Pure parser with xUnit tests per fixture and per rule above.
- Index persisted in plugin data dir; rebuilt by post-scan task.

### Phase 3 — Metadata
- Series/Season/Episode providers: titles, index numbers, studio/year, artwork rules.
- Ignore rule so resource folders and documents don't create empty seasons or junk items.

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
- Quizzes (once a real export sample exists).
- Optional per-course `course.json` override (titles/order/description).
- Course-level "Continue where you left off" button.

## Open risks
- **File Transformation compatibility with 12.2**: verify in Phase 0. Fallback: serve the bundle
  from the plugin and load it through a generic custom-JS plugin.
- **jellyfin-web DOM changes** between releases can break injected UI. Keep selectors isolated
  in one module.
- **MKV in browser** may force transcoding; check the dedicated instance's hardware acceleration.
- **Exact 12.2 extension APIs** (resolver/provider interfaces, event args) to be verified against
  the 12.2 `Jellyfin.Controller` package during the spike.
