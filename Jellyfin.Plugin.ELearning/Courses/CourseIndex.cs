using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// All parsed courses of the course libraries, persisted in the plugin data folder and rebuilt
/// after each library scan. Files are resolved by ID only, never by a client-supplied path.
/// </summary>
public sealed class CourseIndex
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    private readonly string _file;
    private readonly ILogger<CourseIndex> _logger;
    private readonly Lock _lock = new();
    private Snapshot? _snapshot;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseIndex"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="logger">The logger.</param>
    public CourseIndex(IApplicationPaths applicationPaths, ILogger<CourseIndex> logger)
        : this(Path.Combine(applicationPaths?.DataPath ?? throw new ArgumentNullException(nameof(applicationPaths)), "elearning", "course-index.json"), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseIndex"/> class that stores the index in <paramref name="file"/>.
    /// </summary>
    /// <param name="file">The index file.</param>
    /// <param name="logger">The logger.</param>
    internal CourseIndex(string file, ILogger<CourseIndex> logger)
    {
        _file = file;
        _logger = logger;
    }

    /// <summary>
    /// Gets all indexed courses.
    /// </summary>
    public IReadOnlyCollection<IndexedCourse> Courses => Current.ByPath.Values;

    private Snapshot Current
    {
        get
        {
            lock (_lock)
            {
                return _snapshot ??= Load();
            }
        }
    }

    /// <summary>
    /// Gets the course whose folder is <paramref name="coursePath"/> (a Series path).
    /// </summary>
    /// <param name="coursePath">Absolute course folder path.</param>
    /// <returns>The course, or null.</returns>
    public IndexedCourse? GetCourse(string coursePath)
        => Current.ByPath.GetValueOrDefault(Path.TrimEndingDirectorySeparator(coursePath));

    /// <summary>
    /// Resolves a file ID to the file, its course and its absolute path.
    /// </summary>
    /// <param name="fileId">The file ID.</param>
    /// <returns>The file, or null when the ID is unknown or its path would leave the course folder.</returns>
    public IndexedFile? GetFile(string fileId)
    {
        if (fileId is null || !Current.Files.TryGetValue(fileId, out var entry))
        {
            return null;
        }

        // The index file is plain JSON on disk: never trust its relative paths to stay inside the course.
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(entry.Course.Path)) + Path.DirectorySeparatorChar;
        var absolute = Path.GetFullPath(Path.Combine(root, entry.File.RelativePath));
        return absolute.StartsWith(root, StringComparison.Ordinal) && absolute.Length > root.Length
            ? new IndexedFile(entry.Course, entry.File, entry.Lecture, absolute)
            : null;
    }

    /// <summary>
    /// Replaces the whole index and saves it.
    /// </summary>
    /// <param name="courses">The courses.</param>
    public void Replace(IEnumerable<IndexedCourse> courses)
    {
        var list = courses.ToList();
        var snapshot = Snapshot.Create(list);
        lock (_lock)
        {
            _snapshot = snapshot;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(list, _json));
        File.Move(temp, _file, true);
    }

    private Snapshot Load()
    {
        if (!File.Exists(_file))
        {
            return Snapshot.Create([]);
        }

        try
        {
            return Snapshot.Create(JsonSerializer.Deserialize<List<IndexedCourse>>(File.ReadAllText(_file), _json) ?? []);
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Ignoring unreadable course index {File}; it is rebuilt on the next library scan", _file);
            return Snapshot.Create([]);
        }
    }

    private sealed record FileEntry(IndexedCourse Course, CourseFile File, CourseLecture? Lecture);

    private sealed record Snapshot(FrozenDictionary<string, IndexedCourse> ByPath, FrozenDictionary<string, FileEntry> Files)
    {
        public static Snapshot Create(IReadOnlyList<IndexedCourse> courses)
        {
            var files = new Dictionary<string, FileEntry>(StringComparer.Ordinal);
            foreach (var indexed in courses)
            {
                var course = indexed.Course;
                var lectures = course.Sections.SelectMany(s => s.Lectures).ToList();
                var all = course.Resources
                    .Concat(course.Sections.SelectMany(s => s.Resources))
                    .Concat(lectures.SelectMany(l => l.Attachments))
                    .Select(f => new FileEntry(indexed, f, null))
                    .Concat(lectures.Select(l => new FileEntry(indexed, l.ToFile(), l)));
                foreach (var entry in all)
                {
                    files[entry.File.Id] = entry;
                }
            }

            return new Snapshot(
                courses.ToFrozenDictionary(c => Path.TrimEndingDirectorySeparator(c.Path), StringComparer.Ordinal),
                files.ToFrozenDictionary(StringComparer.Ordinal));
        }
    }
}
