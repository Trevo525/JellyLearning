using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Section (Season) number and title. Also names the folder-less season Jellyfin creates for
/// flat courses ("Lectures" instead of "Season 1").
/// </summary>
public class SectionProvider : ICustomMetadataProvider<Season>
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
    {
        ArgumentNullException.ThrowIfNull(item);
        var seriesPath = item.Series?.Path;
        var lookup = _catalog.GetCourseFor(string.IsNullOrEmpty(item.Path) ? seriesPath : item.Path);
        if (lookup is null)
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        var section = string.IsNullOrEmpty(item.Path)
            ? item.IndexNumber is { } number ? lookup.GetSeason(number) : null
            : lookup.GetSection(lookup.Relative(item.Path));
        if (section?.SeasonNumber is null)
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        var changed = false;
        if (item.IndexNumber != section.SeasonNumber)
        {
            item.IndexNumber = section.SeasonNumber;
            changed = true;
        }

        if (!item.IsFieldLocked(MetadataField.Name) && item.Name != section.Title)
        {
            item.Name = section.Title;
            changed = true;
        }

        return Task.FromResult(changed ? ItemUpdateType.MetadataEdit : ItemUpdateType.None);
    }
}
