using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ELearning.Api;

/// <summary>
/// Detects "link stub" HTML files: pages that only redirect to an external site with a
/// <c>meta refresh</c> (common in Udemy <c>N-Resources/</c> folders). They are shown as
/// external links instead of in the viewer.
/// </summary>
public static partial class LinkStub
{
    /// <summary>Files larger than this are never treated as link stubs.</summary>
    public const int MaxBytes = 32 * 1024;

    /// <summary>
    /// Gets the target of a link stub.
    /// </summary>
    /// <param name="html">The HTML source.</param>
    /// <returns>The absolute http(s) URL the page redirects to, or null when it is not a link stub.</returns>
    public static Uri? Detect(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        foreach (Match meta in MetaTag().Matches(html))
        {
            if (!RefreshAttribute().IsMatch(meta.Value))
            {
                continue;
            }

            var content = ContentAttribute().Match(meta.Value);
            var url = content.Success ? RefreshUrl().Match(WebUtility.HtmlDecode(content.Groups["value"].Value)) : Match.Empty;
            if (url.Success
                && Uri.TryCreate(url.Groups["url"].Value.Trim(), UriKind.Absolute, out var target)
                && (target.Scheme == Uri.UriSchemeHttps || target.Scheme == Uri.UriSchemeHttp))
            {
                return target;
            }
        }

        return null;
    }

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaTag();

    [GeneratedRegex(@"\bhttp-equiv\s*=\s*[""']?\s*refresh\b", RegexOptions.IgnoreCase)]
    private static partial Regex RefreshAttribute();

    [GeneratedRegex(@"\bcontent\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex ContentAttribute();

    [GeneratedRegex(@"^\s*\d*(?:\.\d+)?\s*[;,]?\s*url\s*=\s*['""]?(?<url>[^'""]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RefreshUrl();
}
