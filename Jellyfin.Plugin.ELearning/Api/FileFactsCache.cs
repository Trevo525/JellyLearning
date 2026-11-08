using System;
using System.Collections.Concurrent;
using System.IO;
using Jellyfin.Plugin.ELearning.Courses;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Api;

/// <summary>
/// Reads sizes and link-stub targets of course files, re-reading a file only when it changed.
/// </summary>
public sealed class FileFactsCache
{
    private readonly ConcurrentDictionary<string, (DateTime Modified, FileFacts Facts)> _cache = new(StringComparer.Ordinal);
    private readonly ILogger<FileFactsCache> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileFactsCache"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public FileFactsCache(ILogger<FileFactsCache> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the facts about a file.
    /// </summary>
    /// <param name="absolutePath">Absolute path of the file.</param>
    /// <param name="kind">The file kind.</param>
    /// <returns>The facts.</returns>
    public FileFacts Get(string absolutePath, FileKind kind)
    {
        try
        {
            var info = new FileInfo(absolutePath);
            if (!info.Exists)
            {
                return new FileFacts(null, null);
            }

            if (_cache.TryGetValue(absolutePath, out var cached) && cached.Modified == info.LastWriteTimeUtc && cached.Facts.Size == info.Length)
            {
                return cached.Facts;
            }

            var link = info.Length > LinkStub.MaxBytes ? null
                : kind == FileKind.Html ? LinkStub.Detect(File.ReadAllText(absolutePath))
                : kind == FileKind.Link ? LinkStub.FromShortcut(File.ReadAllText(absolutePath))
                : null;
            var facts = new FileFacts(info.Length, link);
            _cache[absolutePath] = (info.LastWriteTimeUtc, facts);
            return facts;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read course file {Path}", absolutePath);
            return new FileFacts(null, null);
        }
    }
}
