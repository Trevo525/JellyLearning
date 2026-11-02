using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ELearning.Courses;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// On a course's first scan Jellyfin builds course/section images before the lecture videos are
/// probed, so <see cref="FirstLectureImageProvider"/> has no frame to grab yet. After the scan,
/// queue an image refresh for every course and section that still has no primary image.
/// </summary>
public sealed class CourseArtworkTask : ILibraryPostScanTask
{
    private readonly CourseLibraries _libraries;
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseArtworkTask"/> class.
    /// </summary>
    /// <param name="libraries">The course libraries.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="providerManager">The provider manager.</param>
    /// <param name="fileSystem">The file system.</param>
    public CourseArtworkTask(CourseLibraries libraries, ILibraryManager libraryManager, IProviderManager providerManager, IFileSystem fileSystem)
    {
        _libraries = libraries;
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var options = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
        {
            // "Default" image mode skips dynamic providers here; FullRefresh without ReplaceAllImages
            // only fills in the missing images.
            MetadataRefreshMode = MetadataRefreshMode.None,
            ImageRefreshMode = MetadataRefreshMode.FullRefresh,
            ReplaceAllImages = false,
        };

        // Post-scan task order is not guaranteed, so list courses from disk rather than the index.
        foreach (var coursePath in _libraries.GetLocations().SelectMany(CourseScanner.ListCourses))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_libraryManager.FindByPath(coursePath, true) is not Series series)
            {
                continue;
            }

            foreach (var item in series.Children.OfType<Season>().Prepend<MediaBrowser.Controller.Entities.BaseItem>(series))
            {
                if (!item.HasImage(ImageType.Primary, 0))
                {
                    _providerManager.QueueRefresh(item.Id, options, RefreshPriority.Low);
                }
            }
        }

        return Task.CompletedTask;
    }
}
