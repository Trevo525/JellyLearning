using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// The optional <c>course.json</c> in a course folder: titles, descriptions and order that replace
/// what the parser derives from file names. Sections are keyed by their folder, lectures by their
/// main file, both relative to the course folder with '/' separators. Anything not listed (and a
/// missing or unreadable file) keeps the parsed value.
/// </summary>
public sealed class CourseOverrides
{
    /// <summary>The file name, looked up in the course folder only.</summary>
    public const string FileName = "course.json";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private IReadOnlyDictionary<string, ItemOverride> _sections = new Dictionary<string, ItemOverride>();
    private IReadOnlyDictionary<string, ItemOverride> _lectures = new Dictionary<string, ItemOverride>();

    /// <summary>Gets the course title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the course description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the studio (provider).</summary>
    public string? Studio { get; init; }

    /// <summary>Gets the year.</summary>
    public int? Year { get; init; }

    /// <summary>Gets the section overrides by folder ("" or "." = the implicit section of loose lectures).</summary>
    public IReadOnlyDictionary<string, ItemOverride> Sections
    {
        get => _sections;
        init => _sections = Normalize(value);
    }

    /// <summary>Gets the lecture overrides by main-file path.</summary>
    public IReadOnlyDictionary<string, ItemOverride> Lectures
    {
        get => _lectures;
        init => _lectures = Normalize(value);
    }

    /// <summary>
    /// Parses the contents of a <c>course.json</c>. Comments and trailing commas are allowed.
    /// </summary>
    /// <param name="json">The file contents.</param>
    /// <returns>The overrides, or null when the text is not a valid JSON object of the expected shape.</returns>
    public static CourseOverrides? TryParse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            return JsonSerializer.Deserialize<CourseOverrides>(json, _json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Gets the override of the section in <paramref name="folder"/>.</summary>
    /// <param name="folder">Section folder relative to the course; empty for the implicit section.</param>
    /// <returns>The override or null.</returns>
    public ItemOverride? ForSection(string folder) => _sections.GetValueOrDefault(folder);

    /// <summary>Gets the override of the lecture whose main file is <paramref name="relativePath"/>.</summary>
    /// <param name="relativePath">Main file relative to the course.</param>
    /// <returns>The override or null.</returns>
    public ItemOverride? ForLecture(string relativePath) => _lectures.GetValueOrDefault(relativePath);

    private static Dictionary<string, ItemOverride> Normalize(IReadOnlyDictionary<string, ItemOverride>? source)
    {
        var result = new Dictionary<string, ItemOverride>(StringComparer.Ordinal);
        foreach (var (key, value) in source ?? new Dictionary<string, ItemOverride>())
        {
            var path = key.Replace('\\', '/').Trim('/');
            result[path == "." ? string.Empty : path] = value ?? new ItemOverride();
        }

        return result;
    }
}
