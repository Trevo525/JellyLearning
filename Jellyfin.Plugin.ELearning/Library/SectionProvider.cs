using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Section (Season) number and title. Also names the folder-less season Jellyfin creates for
/// flat courses ("Lectures" instead of "Season 1").
/// </summary>
public class SectionProvider : ICustomMetadataProvider<Season>, IHasItemChangeMonitor
{
    private readonly CourseCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="SectionProvider"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    public SectionProvider(CourseCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public string Name => "E-Learning";

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Season item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        => Task.FromResult(Apply(item, write: true) ? ItemUpdateType.MetadataEdit : ItemUpdateType.None);

    // Without this, ordinary scans skip custom providers for existing items, so a plugin update
    // that names things differently would only reach them on a "replace all metadata" refresh.

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
        => item is Season typed && Apply(typed, write: false);

    // Brings the item in line with the parser; with write = false only reports whether it would.
    private bool Apply(Season item, bool write)
    {
        ArgumentNullException.ThrowIfNull(item);
        var seriesPath = item.Series?.Path;
        var lookup = _catalog.GetCourseFor(string.IsNullOrEmpty(item.Path) ? seriesPath : item.Path);
        if (lookup is null)
        {
            return false;
        }

        var section = string.IsNullOrEmpty(item.Path)
            ? item.IndexNumber is { } number ? lookup.GetSeason(number) : null
            : lookup.GetSection(lookup.Relative(item.Path));
        if (section?.SeasonNumber is null)
        {
            return false;
        }

        var changed = false;
        if (item.IndexNumber != section.SeasonNumber)
        {
            changed = true;
            if (write)
            {
                item.IndexNumber = section.SeasonNumber;
            }
        }

        if (!item.IsFieldLocked(MetadataField.Name) && item.Name != section.Title)
        {
            changed = true;
            if (write)
            {
                item.Name = section.Title;
            }
        }

        return changed;
    }
}
