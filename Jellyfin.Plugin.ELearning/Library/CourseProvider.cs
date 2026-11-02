using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Course (Series) metadata from the folder name: title, studio, year.
/// Custom providers run after Jellyfin's own parsing and write straight to the item (PLAN.md, Phase 1).
/// </summary>
public class CourseProvider : ICustomMetadataProvider<Series>
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
    public string Name => "E-Learning";

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Series item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lookup = _catalog.GetCourseFor(item.Path);
        if (lookup is null || lookup.Relative(item.Path).Length != 0)
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        var course = lookup.Course;
        var changed = false;
        if (!item.IsFieldLocked(MetadataField.Name) && item.Name != course.Title)
        {
            item.Name = course.Title;
            changed = true;
        }

        if (course.Studio is not null && !item.IsFieldLocked(MetadataField.Studios) && !item.Studios.SequenceEqual([course.Studio]))
        {
            item.Studios = [course.Studio];
            changed = true;
        }

        if (course.Year is not null && item.ProductionYear != course.Year)
        {
            item.ProductionYear = course.Year;
            changed = true;
        }

        return Task.FromResult(changed ? ItemUpdateType.MetadataEdit : ItemUpdateType.None);
    }
}
