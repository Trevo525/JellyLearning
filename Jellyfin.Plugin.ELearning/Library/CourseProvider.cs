using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Course (Series) metadata from the folder name: title, studio, year.
/// Custom providers run after Jellyfin's own parsing and write straight to the item (PLAN.md, Phase 1).
/// </summary>
public class CourseProvider : ICustomMetadataProvider<Series>, IHasItemChangeMonitor
{
    private readonly CourseCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseProvider"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    public CourseProvider(CourseCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public string Name => "JellyLearning";

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Series item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        => Task.FromResult(Apply(item, write: true) ? ItemUpdateType.MetadataEdit : ItemUpdateType.None);

    // Without this, ordinary scans skip custom providers for existing items, so a plugin update
    // that names things differently would only reach them on a "replace all metadata" refresh.

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
        => item is Series typed && Apply(typed, write: false);

    // Brings the item in line with the parser; with write = false only reports whether it would.
    private bool Apply(Series item, bool write)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lookup = _catalog.GetCourseFor(item.Path);
        if (lookup is null || lookup.Relative(item.Path).Length != 0)
        {
            return false;
        }

        var course = lookup.Course;
        var changed = false;
        if (!item.IsFieldLocked(MetadataField.Name) && item.Name != course.Title)
        {
            changed = true;
            if (write)
            {
                item.Name = course.Title;
            }
        }

        if (course.Studio is not null && !item.IsFieldLocked(MetadataField.Studios) && !item.Studios.SequenceEqual([course.Studio]))
        {
            changed = true;
            if (write)
            {
                item.Studios = [course.Studio];
            }
        }

        if (course.Year is not null && item.ProductionYear != course.Year)
        {
            changed = true;
            if (write)
            {
                item.ProductionYear = course.Year;
            }
        }

        if (course.Description is not null && !item.IsFieldLocked(MetadataField.Overview) && item.Overview != course.Description)
        {
            changed = true;
            if (write)
            {
                item.Overview = course.Description;
            }
        }

        return changed;
    }
}
