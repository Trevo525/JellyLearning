# `course.json`: overriding titles, descriptions and order

JellyLearning builds a course from its folder and file names (see PLAN.md, "Parsing rules").
When the names are wrong or unhelpful, put a `course.json` in the **course folder** (the folder
directly under the library, not in a section). Everything in it is optional; whatever you leave
out keeps the value parsed from the names. A course without the file behaves exactly as before.

## Format

```jsonc
{
  "title": "Course title",          // replaces the cleaned-up folder name
  "description": "Shown as overview.",
  "studio": "Udemy",                // provider; replaces the one recognised from the folder name
  "year": 2024,

  "sections": {                     // key = the section's folder, relative to the course folder
    "02 Advanced": { "title": "Harder Things", "description": "…", "order": 1 }
  },

  "lectures": {                     // key = the lecture's main file, relative to the course folder
    "01 Basics/02 Setup.mp4": { "title": "Getting set up", "description": "…", "order": 0.5 }
  }
}
```

Comments (`//`, `/* */`) and trailing commas are allowed. Property names are not case-sensitive.

| Key | Applies to | Effect |
|---|---|---|
| `title` | course, section, lecture | Used exactly as written (no cleaning). Blank is ignored. |
| `description` | course, section, lecture | Becomes the Jellyfin overview of the series, season or episode; the player's Overview tab shows it. |
| `studio`, `year` | course | Replace the parsed values. |
| `order` | section, lecture | Sort key used instead of the number in the name. Any number, fractions allowed. |

## Keys

- Paths are relative to the course folder and use `/` (a `\` is accepted too). They must match the
  folder or file name exactly, including case and extension.
- The loose lectures of a flat course form one implicit section: its key is `"."` (or `""`).
- A lecture is the **main file** (video, or the HTML/PDF/Markdown document). Attachments are not
  addressable.
- Keys that match nothing are ignored.

## Order

Sections and lectures are sorted by `order` where they have one, otherwise by the number in their
name; items with neither go last, then ties by video-before-document and natural title. So to move
lecture 5 before lecture 2, give it `"order": 1.5`; you do not need to number the others.

Season and episode numbers are positions in the final order. They are part of Jellyfin's watch-data
keys, so **changing `order` (or adding a section) after someone has progress in the course can
orphan their resume points**. Set it up before watching.

## Examples

Rename a course whose folder is a scene release name:

```json
{ "title": "Learn to Sketch Anything", "studio": "Udemy", "year": 2025 }
```

Put a bonus lecture first and retitle a bad filename:

```json
{
  "lectures": {
    "05 Section/99 bonus.mp4": { "title": "Bonus: tips", "order": 0 },
    "01 Intro/003 x_y_z.mp4": { "title": "Welcome" }
  }
}
```

Name the lectures of a flat course's single section:

```json
{ "sections": { ".": { "title": "All lectures" } } }
```

## Behaviour

- The file is read on library scans and when the index is rebuilt; **scan the library** after
  editing it. Read-only course folders are fine; the plugin never writes there.
- `course.json` is hidden from the course resources.
- If the file is not valid JSON, or a value has the wrong type (`"year": "soon"`), the whole file is
  ignored and a warning naming it is written to the Jellyfin log.
- Fields you locked in Jellyfin's metadata editor (name, overview) are not overwritten.
- It does not read Kodi/Jellyfin `.nfo` files; those are ignored for course libraries.
