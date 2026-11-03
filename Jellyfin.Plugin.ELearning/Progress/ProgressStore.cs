using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Jellyfin.Plugin.ELearning.Courses;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Progress;

/// <summary>
/// Per-user completion of non-video lectures (HTML, quizzes, documents), which Jellyfin has no
/// items for. One JSON file per user in the plugin data folder, keyed by <see cref="Key"/> (course
/// folder name + relative path, so it survives index rebuilds, reinstalls and moving the library).
/// </summary>
public sealed class ProgressStore
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    private readonly string _folder;
    private readonly ILogger<ProgressStore> _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, Dictionary<string, DateTime>> _cache = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="logger">The logger.</param>
    public ProgressStore(IApplicationPaths applicationPaths, ILogger<ProgressStore> logger)
        : this(Path.Combine(applicationPaths?.DataPath ?? throw new ArgumentNullException(nameof(applicationPaths)), "elearning", "progress"), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressStore"/> class that stores its files in <paramref name="folder"/>.
    /// </summary>
    /// <param name="folder">The folder for the per-user files.</param>
    /// <param name="logger">The logger.</param>
    internal ProgressStore(string folder, ILogger<ProgressStore> logger)
    {
        _folder = folder;
        _logger = logger;
    }

    /// <summary>
    /// Gets the progress key of a lecture. Unlike file IDs it does not depend on where the library is.
    /// </summary>
    /// <param name="courseFolderName">The course folder name.</param>
    /// <param name="relativePath">The lecture's path relative to the course folder.</param>
    /// <returns>The key.</returns>
    public static string Key(string courseFolderName, string relativePath) => CourseNaming.FileId(courseFolderName, relativePath);

    /// <summary>
    /// Gets the keys of the lectures <paramref name="userId"/> has completed.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>Completed lecture keys.</returns>
    public IReadOnlySet<string> GetCompleted(Guid userId)
    {
        lock (_lock)
        {
            return Get(userId).Keys.ToHashSet(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Marks a lecture completed or not completed for a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="lectureKey">The lecture's <see cref="Key"/>.</param>
    /// <param name="completed">Whether the lecture is completed.</param>
    public void SetCompleted(Guid userId, string lectureKey, bool completed)
    {
        ArgumentNullException.ThrowIfNull(lectureKey);
        lock (_lock)
        {
            var entries = Get(userId);
            var changed = completed ? entries.TryAdd(lectureKey, DateTime.UtcNow) : entries.Remove(lectureKey);
            if (changed)
            {
                Directory.CreateDirectory(_folder);
                var file = FileFor(userId);
                File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(new UserProgress { Completed = entries }, _json));
                File.Move(file + ".tmp", file, true);
            }
        }
    }

    private Dictionary<string, DateTime> Get(Guid userId)
    {
        if (_cache.TryGetValue(userId, out var entries))
        {
            return entries;
        }

        entries = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var file = FileFor(userId);
        if (File.Exists(file))
        {
            try
            {
                foreach (var (id, at) in JsonSerializer.Deserialize<UserProgress>(File.ReadAllText(file), _json)?.Completed ?? [])
                {
                    entries[id] = at;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
            {
                // Keep the unreadable file for inspection instead of overwriting it on the next save.
                _logger.LogError(ex, "Could not read lecture progress {File}; moved aside, starting empty", file);
                TryMoveAside(file);
            }
        }

        _cache[userId] = entries;
        return entries;
    }

    private static void TryMoveAside(string file)
    {
        try
        {
            File.Move(file, file + ".corrupt", true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing more to do; the next save replaces it.
        }
    }

    private string FileFor(Guid userId) => Path.Combine(_folder, userId.ToString("N") + ".json");

    private sealed class UserProgress
    {
        public Dictionary<string, DateTime> Completed { get; set; } = [];
    }
}
