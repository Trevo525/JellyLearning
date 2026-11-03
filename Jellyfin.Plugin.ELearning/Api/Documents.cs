using System;
using System.Net;
using Markdig;

namespace Jellyfin.Plugin.ELearning.Api;

/// <summary>
/// Builds the HTML pages the viewer shows for documents, and the Content-Security-Policies they
/// are served with. Pages are meant for a sandboxed iframe; the CSP <c>sandbox</c> directive also
/// gives them an opaque origin when opened directly, so they can never reach the user's session.
/// </summary>
public static class Documents
{
    /// <summary>
    /// CSP for HTML lectures and quizzes: inline scripts run (quizzes need them), but in an opaque
    /// origin, without network access and without forms or frames.
    /// </summary>
    public const string ScriptPolicy =
        "sandbox allow-scripts allow-popups allow-popups-to-escape-sandbox; default-src 'none'; "
        + "script-src 'unsafe-inline'; style-src 'unsafe-inline' *; img-src * data: blob:; media-src * data: blob:; "
        + "font-src * data:; connect-src 'none'; frame-src 'none'; form-action 'none'; base-uri 'none'";

    /// <summary>CSP for everything else served inline: no scripts at all.</summary>
    public const string StaticPolicy =
        "sandbox allow-popups allow-popups-to-escape-sandbox; default-src 'none'; style-src 'unsafe-inline'; "
        + "img-src * data:; media-src 'self'; form-action 'none'";

    private static readonly MarkdownPipeline _markdown = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    /// <summary>Renders Markdown (raw HTML in it is shown as text).</summary>
    /// <param name="markdown">The Markdown source.</param>
    /// <param name="title">The page title.</param>
    /// <returns>A complete HTML page.</returns>
    public static string Markdown(string markdown, string title)
        => Page(title, Markdig.Markdown.ToHtml(markdown ?? string.Empty, _markdown));

    /// <summary>Renders plain text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="title">The page title.</param>
    /// <returns>A complete HTML page.</returns>
    public static string Text(string text, string title)
        => Page(title, "<pre>" + WebUtility.HtmlEncode(text ?? string.Empty) + "</pre>");

    /// <summary>Renders a page that points to an external link.</summary>
    /// <param name="url">The link target.</param>
    /// <param name="title">The page title.</param>
    /// <returns>A complete HTML page.</returns>
    public static string Link(Uri url, string title)
    {
        ArgumentNullException.ThrowIfNull(url);
        var href = WebUtility.HtmlEncode(url.AbsoluteUri);
        return Page(title, $"<h1>{WebUtility.HtmlEncode(title)}</h1><p>This resource is an external link:</p><p><a href=\"{href}\" rel=\"noopener noreferrer\">{href}</a></p>");
    }

    private static string Page(string title, string body)
        => "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            + "<base target=\"_blank\"><title>" + WebUtility.HtmlEncode(title) + "</title><style>"
            + ":root{color-scheme:light dark}body{font-family:system-ui,sans-serif;line-height:1.6;max-width:50em;margin:2em auto;padding:0 1em}"
            + "img{max-width:100%}pre{white-space:pre-wrap;word-break:break-word}code,pre{font-family:ui-monospace,monospace}"
            + "table{border-collapse:collapse}td,th{border:1px solid #8888;padding:.3em .6em}a{word-break:break-all}"
            + "</style></head><body>" + body + "</body></html>";
}
