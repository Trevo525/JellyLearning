using System;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Jellyfin.Plugin.ELearning.Web;

/// <summary>
/// The web UI files (embedded resources): the script injected into jellyfin-web and the course
/// player page with its script, styles and hls.js. Served by name only, versioned by a hash of
/// all of them so browsers fetch each build once.
/// </summary>
public static class WebAssets
{
    /// <summary>The script injected into jellyfin-web.</summary>
    public const string InjectedScript = "elearning.js";

    /// <summary>The course player page.</summary>
    public const string PlayerPage = "player.html";

    private static readonly (string Name, string Resource, string ContentType)[] _files =
    [
        (InjectedScript, "elearning.js", "text/javascript; charset=utf-8"),
        (PlayerPage, "player.html", "text/html; charset=utf-8"),
        ("player.js", "player.js", "text/javascript; charset=utf-8"),
        ("player.css", "player.css", "text/css; charset=utf-8"),
        ("hls.light.min.js", "ThirdParty.hls.light.min.js", "text/javascript; charset=utf-8"),
    ];

    private static readonly Lazy<(FrozenDictionary<string, (byte[] Content, string ContentType)> Files, string Version)> _assets = new(Load);

    /// <summary>Gets a short hash over all files, used as <c>?v=</c>.</summary>
    public static string Version => _assets.Value.Version;

    /// <summary>
    /// Gets a file by name.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>Content and content type, or null for unknown names.</returns>
    public static (byte[] Content, string ContentType)? Get(string name)
        => name is not null && _assets.Value.Files.TryGetValue(name, out var file) ? file : null;

    /// <summary>
    /// Adds the script tag to jellyfin-web's index.html (once).
    /// </summary>
    /// <param name="indexHtml">The original index.html.</param>
    /// <returns>The page with the script tag before <c>&lt;/body&gt;</c>.</returns>
    public static string Inject(string indexHtml)
    {
        ArgumentNullException.ThrowIfNull(indexHtml);
        if (indexHtml.Contains("ELearning/Web/" + InjectedScript, StringComparison.Ordinal))
        {
            return indexHtml;
        }

        // index.html is served from <base>/web/, the API from <base>/, so go one level up.
        var tag = $"<script defer src=\"../ELearning/Web/{InjectedScript}?v={Version}\"></script>";
        var body = indexHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return body < 0 ? indexHtml + tag : indexHtml.Insert(body, tag);
    }

    private static (FrozenDictionary<string, (byte[] Content, string ContentType)> Files, string Version) Load()
    {
        var assembly = typeof(WebAssets).Assembly;
        var files = _files.ToDictionary(
            f => f.Name,
            f =>
            {
                using var stream = assembly.GetManifestResourceStream(typeof(WebAssets).Namespace + "." + f.Resource)
                    ?? throw new InvalidOperationException("Embedded web file is missing: " + f.Resource);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return (Content: memory.ToArray(), f.ContentType);
            },
            StringComparer.Ordinal);

#pragma warning disable CA1308 // Hex version string, lower-case by convention.
        var version = Convert.ToHexString(SHA256.HashData(files.OrderBy(f => f.Key, StringComparer.Ordinal).SelectMany(f => f.Value.Content).ToArray()), 0, 6).ToLowerInvariant();
#pragma warning restore CA1308

        // The player page links its files with the version, so it is the one file that must not be cached.
        var page = files[PlayerPage];
        files[PlayerPage] = (Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(page.Content).Replace("{{version}}", version, StringComparison.Ordinal)), page.ContentType);
        return (files.ToFrozenDictionary(StringComparer.Ordinal), version);
    }
}
