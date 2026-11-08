using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Lecture (Episode) title, section (season) number and position (episode number). Numbers
/// become user-data keys, so they come only from the snapshot-tested parser.
/// </summary>
public class LectureProvider : ICustomMetadataProvider<Episode>, IHasItemChangeMonitor
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
        => Task.FromResult(Apply(item, write: true) ? ItemUpdateType.MetadataEdit : ItemUpdateType.None);

    // Without this, ordinary scans skip custom providers for existing items, so a plugin update
    // that names things differently would only reach them on a "replace all metadata" refresh.

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
        => item is Episode typed && Apply(typed, write: false);

    // Brings the item in line with the parser; with write = false only reports whether it would.
    private bool Apply(Episode item, bool write)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lookup = _catalog.GetCourseFor(item.Path);
        var lecture = lookup?.GetLecture(item.Path);
        if (lookup is null || lecture?.EpisodeNumber is null)
        {
            return false;
        }

        var folder = lookup.Relative(Path.GetDirectoryName(item.Path) ?? lookup.Indexed.Path);
        var seasonNumber = lookup.GetSection(folder)?.SeasonNumber;

        var changed = false;
        if (item.ParentIndexNumber != seasonNumber)
        {
            changed = true;
            if (write)
            {
                item.ParentIndexNumber = seasonNumber;
            }
        }

        if (item.IndexNumber != lecture.EpisodeNumber)
        {
            changed = true;
            if (write)
            {
                item.IndexNumber = lecture.EpisodeNumber;
            }
        }

        if (!item.IsFieldLocked(MetadataField.Name) && item.Name != lecture.Title)
        {
            changed = true;
            if (write)
            {
                item.Name = lecture.Title;
            }
        }

        return changed;
    }
}
