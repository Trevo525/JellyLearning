using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Lecture (Episode) title, section (season) number and position (episode number). Numbers
/// become user-data keys, so they come only from the snapshot-tested parser.
/// </summary>
public class LectureProvider : ICustomMetadataProvider<Episode>
{
    private readonly CourseCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="LectureProvider"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    public LectureProvider(CourseCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public string Name => "E-Learning";

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Episode item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lookup = _catalog.GetCourseFor(item.Path);
        var lecture = lookup?.GetLecture(item.Path);
        if (lookup is null || lecture?.EpisodeNumber is null)
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        var folder = lookup.Relative(Path.GetDirectoryName(item.Path) ?? lookup.Indexed.Path);
        var seasonNumber = lookup.GetSection(folder)?.SeasonNumber;

        var changed = false;
        if (item.ParentIndexNumber != seasonNumber)
        {
            item.ParentIndexNumber = seasonNumber;
            changed = true;
        }

        if (item.IndexNumber != lecture.EpisodeNumber)
        {
            item.IndexNumber = lecture.EpisodeNumber;
            changed = true;
        }

        if (!item.IsFieldLocked(MetadataField.Name) && item.Name != lecture.Title)
        {
            item.Name = lecture.Title;
            changed = true;
        }

        return Task.FromResult(changed ? ItemUpdateType.MetadataEdit : ItemUpdateType.None);
    }
}
