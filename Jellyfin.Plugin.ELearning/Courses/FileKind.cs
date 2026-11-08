namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// What kind of file a course file is, which decides how the web UI offers it.
/// </summary>
public enum FileKind
{
    /// <summary>Anything else (CAD files, executables, ...): download only.</summary>
    Other,

    /// <summary>A playable video.</summary>
    Video,

    /// <summary>A sidecar subtitle (srt, vtt, ass, ssa).</summary>
    Subtitle,

    /// <summary>A PDF document.</summary>
    Pdf,

    /// <summary>An HTML page (lecture, quiz or link stub).</summary>
    Html,

    /// <summary>A Markdown document.</summary>
    Markdown,

    /// <summary>A plain-text file.</summary>
    Text,

    /// <summary>An image.</summary>
    Image,

    /// <summary>A compressed archive (zip, 7z, tar, ...).</summary>
    Archive,

    /// <summary>An office document, spreadsheet or presentation.</summary>
    Document,

    /// <summary>An internet shortcut (<c>.url</c>), shown as an external link.</summary>
    Link,
}
