using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Resolvers;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Makes every video section folder of a course a Season, overriding Jellyfin's rule that
/// rejects folder names which look like episode codes ("15-3D_Simarabo…" = S15E3).
/// </summary>
public class SectionResolver : IItemResolver
{
    private readonly CourseCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="SectionResolver"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    public SectionResolver(CourseCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public ResolverPriority Priority => ResolverPriority.Plugin;

    /// <inheritdoc />
    public BaseItem? ResolvePath(ItemResolveArgs args)
    {
        if (args is null || !args.IsDirectory || args.Parent is not Series series)
        {
            return null;
        }

        var course = _catalog.GetCourseFor(args.Path);
        var section = course?.GetSection(course.Relative(args.Path));
        if (section?.SeasonNumber is not { } seasonNumber || section.RelativePath.Length == 0)
        {
            return null;
        }

        return new Season
        {
            Path = args.Path,
            SeriesId = series.Id,
            SeriesName = series.Name,
            IndexNumber = seasonNumber,
            Name = section.Title,
        };
    }
}
