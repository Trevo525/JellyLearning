using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.ELearning.Courses;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Generated posters (<see cref="CoverRenderer"/>) for courses and sections. A local image provider,
/// so Jellyfin applies it on every refresh without a per-library setting; it runs after Jellyfin's
/// own local images, so a poster/cover/folder image in the course folder still wins.
/// </summary>
public class CourseCoverProvider : ILocalImageProvider, IHasOrder
{
    private readonly CourseCatalog _catalog;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CourseCoverProvider> _logger;
    private readonly string _folder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCoverProvider"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="logger">The logger.</param>
    public CourseCoverProvider(CourseCatalog catalog, IApplicationPaths applicationPaths, IFileSystem fileSystem, ILogger<CourseCoverProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(applicationPaths);
        _catalog = catalog;
        _fileSystem = fileSystem;
        _logger = logger;
        _folder = Path.Combine(applicationPaths.DataPath, "elearning", "covers");
    }

    /// <inheritdoc />
    public string Name => "E-Learning cover";

    /// <inheritdoc />
    public int Order => 1;

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is Series or Season && Describe(item) is not null;

    /// <inheritdoc />
    public IEnumerable<LocalImageInfo> GetImages(BaseItem item, IDirectoryService directoryService)
    {
        var cover = item is null ? null : Describe(item);
        var path = cover is null ? null : GetOrRender(cover.Value);
        return path is null ? [] : [new LocalImageInfo { FileInfo = _fileSystem.GetFileInfo(path), Type = ImageType.Primary }];
    }

    private (string Title, string? Label, string? Footer)? Describe(BaseItem item)
    {
        var season = item as Season;
        var lookup = _catalog.GetCourseFor(string.IsNullOrEmpty(item.Path) ? season?.Series?.Path : item.Path);
        if (lookup is null)
        {
            return null;
        }

        var course = lookup.Course;
        if (season is null)
        {
            // Not for anything below the course folder, and never over the course's own cover.
            return lookup.Relative(item.Path).Length == 0 && course.ArtworkPath is null
                ? (course.Title, null, course.Studio)
                : null;
        }

        var section = string.IsNullOrEmpty(season.Path)
            ? season.IndexNumber is { } number ? lookup.GetSeason(number) : null
            : lookup.GetSection(lookup.Relative(season.Path));
        if (section?.SeasonNumber is null)
        {
            return null;
        }

        // A flat course's one section is the whole course.
        return section.RelativePath.Length == 0
            ? (course.Title, "Lectures", course.Studio)
            : (section.Title, string.Create(CultureInfo.InvariantCulture, $"Section {section.SeasonNumber}"), course.Title);
    }

    private string? GetOrRender((string Title, string? Label, string? Footer) cover)
    {
        var key = string.Join('\n', CoverRenderer.DesignVersion, cover.Title, cover.Label, cover.Footer);
        var path = Path.Combine(_folder, CourseNaming.FileId("cover", key) + ".jpg");
        if (File.Exists(path))
        {
            return path;
        }

        try
        {
            Directory.CreateDirectory(_folder);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, CoverRenderer.Render(cover.Title, cover.Label, cover.Footer));
            File.Move(temp, path, true);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not write the cover for {Title}", cover.Title);
            return null;
        }
    }
}
