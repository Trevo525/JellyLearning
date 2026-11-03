using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.ELearning.Api.Models;
using Jellyfin.Plugin.ELearning.Courses;
using Jellyfin.Plugin.ELearning.Library;
using Jellyfin.Plugin.ELearning.Progress;
using Jellyfin.Plugin.ELearning.Web;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ELearning.Api;

/// <summary>
/// The course API used by the injected web UI: outlines with progress, course files, rendered
/// documents and completion of non-video lectures. Files are addressed only by their index ID.
/// </summary>
[ApiController]
[Authorize]
[Route("ELearning")]
public sealed class ELearningController : ControllerBase
{
    // Documents rendered in memory (Markdown, text, HTML lectures) are capped at this size.
    private const long MaxRenderBytes = 8 * 1024 * 1024;

    private readonly CourseIndex _index;
    private readonly CourseLibraries _libraries;
    private readonly ProgressStore _progress;
    private readonly FileFactsCache _facts;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ELearningController"/> class.
    /// </summary>
    /// <param name="index">The course index.</param>
    /// <param name="libraries">The course libraries.</param>
    /// <param name="progress">The document lecture progress store.</param>
    /// <param name="facts">The file facts cache.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="authorizationContext">The authorization context.</param>
    public ELearningController(
        CourseIndex index,
        CourseLibraries libraries,
        ProgressStore progress,
        FileFactsCache facts,
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IUserManager userManager,
        IAuthorizationContext authorizationContext)
    {
        _index = index;
        _libraries = libraries;
        _progress = progress;
        _facts = facts;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _authorizationContext = authorizationContext;
    }

    /// <summary>
    /// Gets the outline of a course with the user's progress.
    /// </summary>
    /// <param name="itemId">The course's series ID, or the ID of one of its seasons or episodes.</param>
    /// <param name="userId">Optional user (API keys and administrators only).</param>
    /// <response code="200">Outline returned.</response>
    /// <response code="404">Not a course, or not visible to the user.</response>
    /// <returns>The outline.</returns>
    [HttpGet("Courses/{itemId}/Outline")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseOutline>> GetOutline([FromRoute, Required] Guid itemId, [FromQuery] Guid? userId)
    {
        var user = await GetUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return BadRequest("A user is required.");
        }

        var series = _libraryManager.GetItemById(itemId) switch
        {
            Series s => s,
            Season s => s.Series,
            Episode e => e.Series,
            _ => null,
        };
        var course = series?.Path is { } path ? _index.GetCourse(path) : null;
        if (series is null || course is null || !IsAccessible(course, series, user))
        {
            return NotFound();
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            AncestorIds = [series.Id],
            IncludeItemTypes = [BaseItemKind.Season, BaseItemKind.Episode],
            IsVirtualItem = false,
        });
        var episodes = items.OfType<Episode>().Where(e => e.Path is not null).ToList();
        var userData = _userDataManager.GetUserDataBatch(episodes, user);
        var episodeStates = new Dictionary<string, EpisodeState>(StringComparer.Ordinal);
        foreach (var episode in episodes)
        {
            var data = userData.GetValueOrDefault(episode.Id);
            episodeStates[Relative(course, episode.Path)] = new EpisodeState(
                episode.Id,
                data?.Played ?? false,
                data?.PlaybackPositionTicks ?? 0,
                episode.RunTimeTicks);
        }

        var seasons = items.OfType<Season>()
            .Where(s => s.IndexNumber is not null)
            .GroupBy(s => s.IndexNumber!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);

        return OutlineBuilder.Build(
            series.Id,
            course.Course,
            episodeStates,
            seasons,
            _progress.GetCompleted(user.Id),
            (relativePath, kind) => _facts.Get(Path.Combine(course.Path, relativePath), kind));
    }

    /// <summary>
    /// Gets a course file (lecture, attachment or resource), with range support.
    /// </summary>
    /// <param name="fileId">The file ID from the outline.</param>
    /// <param name="download">Whether to send it as an attachment.</param>
    /// <param name="userId">Optional user (API keys and administrators only).</param>
    /// <response code="200">File returned.</response>
    /// <response code="404">Unknown ID, missing file, or not visible to the user.</response>
    /// <returns>The file.</returns>
    [HttpGet("Files/{fileId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetFile([FromRoute, Required] string fileId, [FromQuery] bool download = false, [FromQuery] Guid? userId = null)
    {
        var file = await GetAccessibleFileAsync(fileId, userId).ConfigureAwait(false);
        if (file is null)
        {
            return NotFound();
        }

        var contentType = MimeTypes.GetMimeType(file.AbsolutePath);
        Response.Headers.XContentTypeOptions = "nosniff";

        // Browsers won't show PDFs in a sandboxed document; their viewer runs isolated anyway.
        if (contentType != MediaTypeNames.Application.Pdf)
        {
            Response.Headers.ContentSecurityPolicy = Documents.StaticPolicy;
        }

        return PhysicalFile(file.AbsolutePath, contentType, download ? file.File.DisplayName : null, enableRangeProcessing: true);
    }

    /// <summary>
    /// Gets a document as a page for the viewer's sandboxed iframe: Markdown and text rendered to
    /// HTML, HTML lectures and quizzes as they are (scripts allowed, see <see cref="Documents.ScriptPolicy"/>),
    /// link stubs as a link page. Must be fetched with the token in a header, never in the URL,
    /// because the page's scripts could read it from there.
    /// </summary>
    /// <param name="fileId">The file ID from the outline.</param>
    /// <param name="userId">Optional user (API keys and administrators only).</param>
    /// <response code="200">Page returned.</response>
    /// <response code="400">The file is not a document, or the token was passed in the URL.</response>
    /// <response code="404">Unknown ID, missing file, or not visible to the user.</response>
    /// <returns>The page.</returns>
    [HttpGet("Files/{fileId}/Render")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Render([FromRoute, Required] string fileId, [FromQuery] Guid? userId = null)
    {
        if (Request.Query.ContainsKey("api_key") || Request.Query.ContainsKey("ApiKey"))
        {
            return BadRequest("Send the token in a header; rendered pages can run scripts that could read the URL.");
        }

        var file = await GetAccessibleFileAsync(fileId, userId).ConfigureAwait(false);
        if (file is null)
        {
            return NotFound();
        }

        var kind = file.File.Kind;
        if (kind is not (FileKind.Html or FileKind.Markdown or FileKind.Text))
        {
            return BadRequest("Only HTML, Markdown and text files are rendered; get other files directly.");
        }

        if (new FileInfo(file.AbsolutePath).Length > MaxRenderBytes)
        {
            return BadRequest("The file is too large to render; download it instead.");
        }

        var title = file.Lecture?.Title ?? Path.GetFileNameWithoutExtension(file.File.DisplayName);
        Response.Headers.XContentTypeOptions = "nosniff";
        if (kind == FileKind.Html)
        {
            if (_facts.Get(file.AbsolutePath, kind).ExternalUrl is { } link)
            {
                return Page(Documents.Link(link, title), Documents.StaticPolicy);
            }

            Response.Headers.ContentSecurityPolicy = Documents.ScriptPolicy;
            return PhysicalFile(file.AbsolutePath, MediaTypeNames.Text.Html);
        }

        var text = await System.IO.File.ReadAllTextAsync(file.AbsolutePath, HttpContext.RequestAborted).ConfigureAwait(false);
        return Page(kind == FileKind.Markdown ? Documents.Markdown(text, title) : Documents.Text(text, title), Documents.StaticPolicy);
    }

    /// <summary>
    /// Marks a non-video lecture completed for the user. (Video lectures use Jellyfin's played state.)
    /// </summary>
    /// <param name="lectureId">The lecture ID from the outline.</param>
    /// <param name="userId">Optional user (API keys and administrators only).</param>
    /// <response code="204">Completed.</response>
    /// <response code="400">The lecture is a video.</response>
    /// <response code="404">Unknown lecture, or not visible to the user.</response>
    /// <returns>No content.</returns>
    [HttpPost("Lectures/{lectureId}/Complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> MarkComplete([FromRoute, Required] string lectureId, [FromQuery] Guid? userId)
        => SetComplete(lectureId, userId, true);

    /// <summary>
    /// Marks a non-video lecture not completed for the user.
    /// </summary>
    /// <param name="lectureId">The lecture ID from the outline.</param>
    /// <param name="userId">Optional user (API keys and administrators only).</param>
    /// <response code="204">Not completed.</response>
    /// <response code="400">The lecture is a video.</response>
    /// <response code="404">Unknown lecture, or not visible to the user.</response>
    /// <returns>No content.</returns>
    [HttpDelete("Lectures/{lectureId}/Complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> MarkIncomplete([FromRoute, Required] string lectureId, [FromQuery] Guid? userId)
        => SetComplete(lectureId, userId, false);

    /// <summary>
    /// Gets the course UI script that File Transformation injects into jellyfin-web.
    /// </summary>
    /// <response code="200">Script returned.</response>
    /// <returns>The script.</returns>
    [HttpGet("Web/elearning.js")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public FileContentResult GetWebBundle()
    {
        // Requested with ?v=<content hash>, so it can be cached for good.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(WebBundle.Content.ToArray(), "text/javascript; charset=utf-8");
    }

    private static string Relative(IndexedCourse course, string path)
        => Path.GetRelativePath(course.Path, path).Replace('\\', '/');

    private async Task<ActionResult> SetComplete(string lectureId, Guid? userId, bool completed)
    {
        var user = await GetUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return BadRequest("A user is required.");
        }

        var file = _index.GetFile(lectureId);
        if (file?.Lecture is null || !IsAccessible(file.Course, null, user))
        {
            return NotFound();
        }

        if (file.Lecture.Kind == FileKind.Video)
        {
            return BadRequest("Video lectures are completed by playing them (Jellyfin's played state).");
        }

        _progress.SetCompleted(user.Id, ProgressStore.Key(file.Course.Course.FolderName, file.Lecture.RelativePath), completed);
        return NoContent();
    }

    private async Task<IndexedFile?> GetAccessibleFileAsync(string fileId, Guid? userId)
    {
        var user = await GetUserAsync(userId).ConfigureAwait(false);
        var file = user is null ? null : _index.GetFile(fileId);
        return file is not null && IsAccessible(file.Course, null, user!) && System.IO.File.Exists(file.AbsolutePath) ? file : null;
    }

    // A course is accessible when it is still in a ticked course library and the user may see its
    // series (library access, parental rating, tags), the same check Jellyfin applies to items.
    private bool IsAccessible(IndexedCourse course, BaseItem? series, User user)
    {
        if (!string.Equals(_libraries.GetCoursePath(course.Path), course.Path, StringComparison.Ordinal))
        {
            return false;
        }

        series ??= _libraryManager.FindByPath(course.Path, true);
        return series is Series && series.IsVisibleStandalone(user);
    }

    // The requesting user; another user only for API keys and administrators (like Jellyfin's own endpoints).
    private async Task<User?> GetUserAsync(Guid? userId)
    {
        var auth = await _authorizationContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
        if (userId is not { } requested || requested == Guid.Empty || requested == auth.UserId)
        {
            return auth.User;
        }

        return auth.IsApiKey || auth.User?.HasPermission(PermissionKind.IsAdministrator) == true
            ? _userManager.GetUserById(requested)
            : null;
    }

    private ContentResult Page(string html, string policy)
    {
        Response.Headers.ContentSecurityPolicy = policy;
        return Content(html, "text/html; charset=utf-8");
    }
}
