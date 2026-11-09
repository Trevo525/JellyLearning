using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// Turns the file listing of one course folder into a <see cref="Course"/>. Pure: works on
/// relative paths only, so it can be tested without a file system (see PLAN.md, "Parsing rules").
/// </summary>
public static class CourseParser
{
    /// <summary>Title of the implicit section that holds lectures lying directly in the course folder.</summary>
    public const string ImplicitSectionTitle = "Lectures";

    /// <summary>
    /// Parses a course.
    /// </summary>
    /// <param name="folderName">Name of the course folder.</param>
    /// <param name="relativeFilePaths">All files below the course folder, relative to it ('/' or '\' separators).</param>
    /// <param name="idScope">What file IDs are unique within (default: the folder name). The scanner passes the
    /// absolute course path, so the same course in two libraries gets distinct IDs.</param>
    /// <param name="overrides">The course's <c>course.json</c>, if it has one.</param>
    /// <returns>The course.</returns>
    public static Course Parse(string folderName, IEnumerable<string> relativeFilePaths, string? idScope = null, CourseOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        ArgumentNullException.ThrowIfNull(relativeFilePaths);

        var files = relativeFilePaths
            .Select(p => p.Replace('\\', '/').Trim('/'))
            .Where(p => p.Length > 0 && !p.Split('/').Any(CourseNaming.IsJunk))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var rootFiles = files.Where(p => !p.Contains('/', StringComparison.Ordinal)).ToList();
        var folders = files
            .Where(p => p.Contains('/', StringComparison.Ordinal))
            .GroupBy(p => p[..p.IndexOf('/', StringComparison.Ordinal)], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(p => p[(g.Key.Length + 1)..]).ToList(), StringComparer.Ordinal);

        var ctx = new Context(idScope ?? folderName, overrides);
        var artwork = rootFiles.Where(CourseNaming.IsArtwork).Order(NaturalComparer.Instance).FirstOrDefault();
        if (artwork is not null)
        {
            rootFiles.Remove(artwork);
        }

        var drafts = new List<SectionDraft>();
        var courseResources = new List<CourseFile>();

        foreach (var (name, children) in folders.OrderBy(f => f.Key, NaturalComparer.Instance))
        {
            if (IsSection(name, children))
            {
                drafts.Add(BuildSection(ctx, name, name, children, courseResources));
            }
            else
            {
                courseResources.AddRange(children.Order(NaturalComparer.Instance).Select(c => ctx.File(name + "/" + c)));
            }
        }

        drafts = drafts
            .OrderBy(d => d.SortKey)
            .ThenBy(d => d.FolderName, NaturalComparer.Instance)
            .ToList();

        // Loose videos in the course folder form an implicit section, placed first.
        if (rootFiles.Any(f => CourseNaming.GetKind(f) == FileKind.Video))
        {
            var looseResources = new List<CourseFile>();
            drafts.Insert(0, BuildSection(ctx, string.Empty, ImplicitSectionTitle, rootFiles, looseResources));
            courseResources.InsertRange(0, looseResources);
        }
        else
        {
            courseResources.InsertRange(0, rootFiles.Order(NaturalComparer.Instance).Select(ctx.File));
        }

        var seasonNumber = 0;
        var sections = drafts
            .Select(d => d.ToSection(d.Lectures.Any(l => l.Kind == FileKind.Video) ? ++seasonNumber : null))
            .ToList();

        var (title, studio, year) = CourseNaming.ParseCourseName(folderName);
        return new Course(
            folderName,
            Text(overrides?.Title) ?? title,
            Text(overrides?.Studio) ?? studio,
            overrides?.Year ?? year,
            artwork,
            sections,
            courseResources,
            Text(overrides?.Description));
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // A folder directly under the course is a section when it holds videos, or when it is numbered
    // and holds numbered lecture documents (e.g. Udemy quiz-only sections). Otherwise it is a
    // resources folder ("Course Resources", "Guidebook").
    private static bool IsSection(string folderName, List<string> children)
    {
        var direct = children.Where(c => !c.Contains('/', StringComparison.Ordinal)).ToList();
        if (direct.Any(c => CourseNaming.GetKind(c) == FileKind.Video))
        {
            return true;
        }

        return CourseNaming.SplitNumber(folderName).Number is not null
            && direct.Any(c => CourseNaming.IsLectureKind(CourseNaming.GetKind(c))
                && CourseNaming.SplitNumber(Path.GetFileNameWithoutExtension(c)).Number is not null);
    }

    private static SectionDraft BuildSection(Context ctx, string folder, string folderName, List<string> children, List<CourseFile> courseResources)
    {
        var isImplicit = folder.Length == 0;
        string Rel(string child) => isImplicit ? child : folder + "/" + child;

        var direct = children.Where(c => !c.Contains('/', StringComparison.Ordinal)).Order(NaturalComparer.Instance).ToList();
        var subfolders = children
            .Where(c => c.Contains('/', StringComparison.Ordinal))
            .GroupBy(c => c[..c.IndexOf('/', StringComparison.Ordinal)], StringComparer.Ordinal)
            .OrderBy(g => g.Key, NaturalComparer.Instance);

        // Unnumbered loose files of the implicit section are course resources, not section resources.
        var resources = isImplicit ? courseResources : new List<CourseFile>();

        var videos = direct
            .Where(c => CourseNaming.GetKind(c) == FileKind.Video)
            .Select(c => ctx.Lecture(Rel(c), FileKind.Video, Path.GetFileNameWithoutExtension(c)))
            .ToList();

        foreach (var file in direct.Where(c => CourseNaming.GetKind(c) == FileKind.Subtitle))
        {
            var owner = videos
                .Select(v => (Video: v, Matched: CourseNaming.TryMatchSubtitle(v.Stem, file, out var language), Language: language))
                .Where(m => m.Matched)
                .OrderByDescending(m => m.Video.Stem.Length)
                .FirstOrDefault();
            if (owner.Video is not null)
            {
#pragma warning disable CA1308 // Formats are lower-case extensions by convention.
                owner.Video.Subtitles.Add(new SubtitleTrack(Rel(file), owner.Language, Path.GetExtension(file).TrimStart('.').ToLowerInvariant()));
#pragma warning restore CA1308
            }
            else
            {
                resources.Add(ctx.File(Rel(file)));
            }
        }

        var lectures = new List<LectureDraft>(videos);
        foreach (var file in direct.Where(c => CourseNaming.GetKind(c) is not (FileKind.Video or FileKind.Subtitle)))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var number = CourseNaming.SplitNumber(stem).Number;
            var kind = CourseNaming.GetKind(file);
            var owner = number is null ? null : FindAttachmentOwner(videos, number.Value, stem, kind);
            if (owner is not null)
            {
                owner.Attachments.Add(ctx.File(Rel(file)));
            }
            else if (number is not null && CourseNaming.IsLectureKind(kind))
            {
                lectures.Add(ctx.Lecture(Rel(file), kind, stem));
            }
            else
            {
                resources.Add(ctx.File(Rel(file)));
            }
        }

        foreach (var sub in subfolders)
        {
            var files = sub.Order(NaturalComparer.Instance).Select(c => ctx.File(Rel(c))).ToList();
            var lectureNumber = CourseNaming.GetResourcesFolderNumber(sub.Key);
            var owner = lectureNumber is null
                ? null
                : lectures.Where(l => l.Number == lectureNumber).OrderBy(l => l.Kind == FileKind.Video ? 0 : 1).FirstOrDefault();
            (owner?.Attachments ?? resources).AddRange(files);
        }

        var ordered = lectures
            .OrderBy(l => l.SortKey)
            .ThenBy(l => l.Kind == FileKind.Video ? 0 : 1)
            .ThenBy(l => l.Stem, NaturalComparer.Instance)
            .ToList();

        var sectionNumber = isImplicit ? null : CourseNaming.SplitNumber(folderName).Number;
        var over = ctx.Overrides?.ForSection(folder);
        return new SectionDraft(
            sectionNumber,
            Text(over?.Title) ?? (isImplicit ? ImplicitSectionTitle : CourseNaming.CleanTitle(folderName)),
            folder,
            folderName,
            ordered,
            isImplicit ? [] : resources,
            over?.Order ?? sectionNumber ?? double.MaxValue,
            Text(over?.Description));
    }

    // A numbered non-video file belongs to the video with the same number when it is not a readable
    // lecture document (zip, xlsx, ...), or when its title extends the video's ("03 Setup - slides.pdf").
    private static LectureDraft? FindAttachmentOwner(List<LectureDraft> videos, int number, string stem, FileKind kind)
    {
        var candidates = videos.Where(v => v.Number == number).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var title = Normalize(CourseNaming.CleanTitle(stem));
        var byTitle = candidates.FirstOrDefault(v =>
        {
            var videoTitle = Normalize(CourseNaming.CleanTitle(v.Stem));
            return title.StartsWith(videoTitle, StringComparison.Ordinal) || videoTitle.StartsWith(title, StringComparison.Ordinal);
        });

        return byTitle ?? (CourseNaming.IsLectureKind(kind) ? null : candidates[0]);
    }

    private static string Normalize(string title)
        => new string(title.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private sealed class Context(string idScope, CourseOverrides? overrides)
    {
        public CourseOverrides? Overrides { get; } = overrides;

        public LectureDraft Lecture(string relativePath, FileKind kind, string stem)
            => new(Id(relativePath), kind, stem, relativePath, Overrides?.ForLecture(relativePath));

        public string Id(string relativePath) => CourseNaming.FileId(idScope, relativePath);

        public CourseFile File(string relativePath)
            => new(Id(relativePath), relativePath, CourseNaming.DisplayName(Path.GetFileName(relativePath)), CourseNaming.GetKind(relativePath));
    }

    private sealed class LectureDraft(string id, FileKind kind, string stem, string relativePath, ItemOverride? over)
    {
        public double SortKey { get; } = over?.Order ?? CourseNaming.SplitNumber(stem).Number ?? double.MaxValue;

        public string? Title { get; } = Text(over?.Title);

        public string? Description { get; } = Text(over?.Description);

        public string Id { get; } = id;

        public FileKind Kind { get; } = kind;

        public string Stem { get; } = stem;

        public int? Number { get; } = CourseNaming.SplitNumber(stem).Number;

        public string RelativePath { get; } = relativePath;

        public List<CourseFile> Attachments { get; } = [];

        public List<SubtitleTrack> Subtitles { get; } = [];
    }

    private sealed record SectionDraft(int? Number, string Title, string RelativePath, string FolderName, List<LectureDraft> Lectures, List<CourseFile> Resources, double SortKey, string? Description)
    {
        public CourseSection ToSection(int? seasonNumber)
        {
            var episode = 0;
            var lectures = Lectures
                .Select(l => new CourseLecture(
                    l.Id,
                    l.Kind,
                    l.Number,
                    l.Title ?? CourseNaming.CleanTitle(l.Stem),
                    l.RelativePath,
                    l.Kind == FileKind.Video ? ++episode : null,
                    l.Attachments,
                    l.Subtitles,
                    l.Description))
                .ToList();
            return new CourseSection(Number, Title, RelativePath, seasonNumber, lectures, Resources, Description);
        }
    }
}
