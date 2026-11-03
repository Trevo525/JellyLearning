using System;
using System.IO;
using System.Security.Cryptography;

namespace Jellyfin.Plugin.ELearning.Web;

/// <summary>
/// The injected web UI script (embedded resource), with a content hash for cache busting.
/// </summary>
public static class WebBundle
{
    private static readonly Lazy<(byte[] Content, string Version)> _bundle = new(Load);

    /// <summary>Gets the script.</summary>
    public static ReadOnlyMemory<byte> Content => _bundle.Value.Content;

    /// <summary>Gets a short hash of the script, used as <c>?v=</c> so browsers fetch each new build once.</summary>
    public static string Version => _bundle.Value.Version;

    /// <summary>
    /// Adds the script tag to jellyfin-web's index.html (once).
    /// </summary>
    /// <param name="indexHtml">The original index.html.</param>
    /// <returns>The page with the script tag before <c>&lt;/body&gt;</c>.</returns>
    public static string Inject(string indexHtml)
    {
        ArgumentNullException.ThrowIfNull(indexHtml);
        if (indexHtml.Contains("ELearning/Web/elearning.js", StringComparison.Ordinal))
        {
            return indexHtml;
        }

        // index.html is served from <base>/web/, the API from <base>/, so go one level up.
        var tag = $"<script defer src=\"../ELearning/Web/elearning.js?v={Version}\"></script>";
        var body = indexHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return body < 0 ? indexHtml + tag : indexHtml.Insert(body, tag);
    }

    private static (byte[] Content, string Version) Load()
    {
        var type = typeof(WebBundle);
        using var stream = type.Assembly.GetManifestResourceStream(type.Namespace + ".elearning.js")
            ?? throw new InvalidOperationException("Embedded web bundle is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var content = memory.ToArray();
#pragma warning disable CA1308 // Hex version string, lower-case by convention.
        return (content, Convert.ToHexString(SHA256.HashData(content), 0, 6).ToLowerInvariant());
#pragma warning restore CA1308
    }
}
