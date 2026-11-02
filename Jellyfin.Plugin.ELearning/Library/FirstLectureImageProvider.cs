using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using Jellyfin.Plugin.ELearning.Courses;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Course and section artwork when the folder has no poster/cover/folder image: a frame from the
/// first video lecture (10 % in). Local images always win (they are applied before this runs).
/// Enable it per library: Library → Manage → image fetchers for Shows/Seasons.
/// </summary>
public class FirstLectureImageProvider : IDynamicImageProvider, IHasOrder
{
    private readonly CourseCatalog _catalog;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly IMediaEncoder _mediaEncoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="FirstLectureImageProvider"/> class.
    /// </summary>
    /// <param name="catalog">The course catalog.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="mediaSourceManager">The media source manager.</param>
    /// <param name="mediaEncoder">The media encoder.</param>
    public FirstLectureImageProvider(CourseCatalog catalog, ILibraryManager libraryManager, IMediaSourceManager mediaSourceManager, IMediaEncoder mediaEncoder)
    {
        _catalog = catalog;
        _libraryManager = libraryManager;
        _mediaSourceManager = mediaSourceManager;
        _mediaEncoder = mediaEncoder;
    }

    /// <inheritdoc />
    public string Name => "E-Learning first lecture";

    /// <inheritdoc />
    public int Order => 100;

    /// <inheritdoc />
    // Jellyfin lists a library's available image fetchers by asking about a blank item; say yes so
    // the fetcher shows up (and survives saving the library settings in the UI).
    public bool Supports(BaseItem item)
        => item is Series or Season
            && (item.Id.IsEmpty() || _catalog.GetCourseFor(string.IsNullOrEmpty(item.Path) ? (item as Season)?.Series?.Path : item.Path) is not null);

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    /// <inheritdoc />
    public async Task<DynamicImageResponse> GetImage(BaseItem item, ImageType type, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var video = FindFirstVideo(item);
        if (video?.DefaultVideoStreamIndex is null)
        {
            return new DynamicImageResponse { HasImage = false };
        }

        var streams = _mediaSourceManager.GetMediaStreams(new MediaStreamQuery { ItemId = video.Id, Type = MediaStreamType.Video });
        var videoStream = streams.Count > 0 ? streams[0] : null;
        if (videoStream is null)
        {
            return new DynamicImageResponse { HasImage = false };
        }

        var offset = video.RunTimeTicks > 0 ? TimeSpan.FromTicks(video.RunTimeTicks.Value / 10) : TimeSpan.FromSeconds(10);
        var mediaSource = new MediaSourceInfo { VideoType = video.VideoType, Protocol = MediaProtocol.File };
        var path = await _mediaEncoder.ExtractVideoImage(video.Path, video.Container, mediaSource, videoStream, video.Video3DFormat, offset, cancellationToken).ConfigureAwait(false);
        return new DynamicImageResponse { HasImage = true, Path = path, Format = ImageFormat.Jpg, Protocol = MediaProtocol.File };
    }

    private Video? FindFirstVideo(BaseItem item)
    {
        var seasonPath = item is Season season && string.IsNullOrEmpty(season.Path) ? season.Series?.Path : item.Path;
        var lookup = _catalog.GetCourseFor(seasonPath);
        if (lookup is null)
        {
            return null;
        }

        var sections = item switch
        {
            Season { Path: { Length: > 0 } path } => [lookup.GetSection(lookup.Relative(path))],
            Season { IndexNumber: { } number } => [lookup.GetSeason(number)],
            _ => lookup.Course.Sections.Cast<CourseSection?>().ToList(),
        };

        var lecture = sections
            .OfType<CourseSection>()
            .Where(s => s.SeasonNumber is not null)
            .SelectMany(s => s.Lectures)
            .FirstOrDefault(l => l.EpisodeNumber is not null);
        return lecture is null
            ? null
            : _libraryManager.FindByPath(Path.Combine(lookup.Indexed.Path, lecture.RelativePath), false) as Video;
    }
}
