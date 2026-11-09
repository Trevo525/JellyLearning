using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Jellyfin only finds sidecar subtitles named <c>video.lang.ext</c>, but Udemy exports use
/// <c>video_lang.ext</c>. Before media probing, copy such subtitles into the item's internal
/// metadata folder under the name Jellyfin expects; the probe then lists them as external streams.
/// Course folders are never written to (they may be read-only).
/// </summary>
public class SubtitleSidecarProvider : ICustomMetadataProvider<Episode>, IPreRefreshProvider, IHasOrder
{
    private readonly CourseCatalog _catalog;
    private readonly ILogger<SubtitleSidecarProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleSidecarProvider"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    /// <param name="logger">The logger.</param>
    public SubtitleSidecarProvider(CourseCatalog catalog, ILogger<SubtitleSidecarProvider> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "JellyLearning subtitles";

    /// <inheritdoc />
    // Before Jellyfin's probe provider (Order 100), which collects external subtitle streams.
    public int Order => 10;

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Episode item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lookup = _catalog.GetCourseFor(item.Path);
        var lecture = lookup?.GetLecture(item.Path);
        if (lookup is null || lecture is null || lecture.Subtitles.Count == 0)
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        var stem = Path.GetFileNameWithoutExtension(item.Path);
        var target = item.GetInternalMetadataPath();
        foreach (var subtitle in lecture.Subtitles)
        {
            var source = Path.Combine(lookup.Indexed.Path, subtitle.RelativePath);
            var name = Path.GetFileNameWithoutExtension(source);
            if (name.Length == stem.Length || name[stem.Length] == '.')
            {
                continue; // Jellyfin already finds it next to the video.
            }

            var destination = Path.Combine(target, stem + (subtitle.Language is null ? string.Empty : "." + subtitle.Language) + Path.GetExtension(source));
            try
            {
                if (!File.Exists(destination) || File.GetLastWriteTimeUtc(destination) < File.GetLastWriteTimeUtc(source))
                {
                    Directory.CreateDirectory(target);
                    File.Copy(source, destination, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not expose subtitle {Source}", source);
            }
        }

        return Task.FromResult(ItemUpdateType.None);
    }
}
