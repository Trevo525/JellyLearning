using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Resolves each video lecture of a course folder on its own, ahead of Jellyfin's folder-wide video
/// resolver. That resolver groups episodes whose path parses to the same SxxEyy into versions of one
/// episode — and a folder name like "Sourdough 2024-6" makes every file in it "S2024E6" — and it silently
/// drops any file with the word "sample" in its name.
/// </summary>
public class LectureResolver : IItemResolver, IMultiItemResolver
{
    private readonly CourseCatalog _catalog;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LectureResolver"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    /// <param name="libraryManager">The library manager.</param>
    public LectureResolver(CourseCatalog catalog, ILibraryManager libraryManager)
    {
        _catalog = catalog;
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public ResolverPriority Priority => ResolverPriority.Plugin;

    /// <inheritdoc />
    public BaseItem? ResolvePath(ItemResolveArgs args) => null;

    /// <inheritdoc />
    public MultiItemResolverResult ResolveMultiple(Folder parent, List<FileSystemMetadata> files, CollectionType? collectionType, IDirectoryService directoryService)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(files);

        // Jellyfin treats a null or empty result as "not mine" and moves on to the next resolver.
        var course = _catalog.GetCourseFor(parent.Path);
        if (course is null)
        {
            return null!;
        }

        var result = new MultiItemResolverResult();
        foreach (var file in files)
        {
            // The per-file path ends in Jellyfin's EpisodeResolver, which neither groups nor drops.
            var item = !file.IsDirectory && course.GetLecture(file.FullName) is not null
                ? _libraryManager.ResolvePath(file, parent, directoryService, collectionType)
                : null;
            if (item is null)
            {
                result.ExtraFiles.Add(file);
            }
            else
            {
                result.Items.Add(item);
            }
        }

        return result;
    }
}
