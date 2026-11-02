using System;
using Jellyfin.Plugin.ELearning.Courses;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Keeps Jellyfin's view of a course identical to the parser's: only video section folders and
/// video lectures become items. Resource folders, lecture resource folders and stray videos are
/// left to the plugin's own file API.
/// </summary>
public class CourseIgnoreRule : IResolverIgnoreRule
{
    private readonly CourseCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseIgnoreRule"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    public CourseIgnoreRule(CourseCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public bool ShouldIgnore(FileSystemMetadata fileInfo, BaseItem? parent)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        var course = _catalog.GetCourseFor(fileInfo.FullName);
        if (course is null)
        {
            return false;
        }

        var relative = course.Relative(fileInfo.FullName);
        if (relative.Length == 0)
        {
            return false;
        }

        if (fileInfo.IsDirectory)
        {
            return course.GetSection(relative)?.SeasonNumber is null;
        }

        return CourseNaming.GetKind(fileInfo.Name) == FileKind.Video
            && course.GetLecture(fileInfo.FullName) is null;
    }
}
