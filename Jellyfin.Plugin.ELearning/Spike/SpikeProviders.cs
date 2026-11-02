// PHASE 1 SPIKE — throwaway. Proves custom metadata providers can renumber courses.
// Replaced by CourseParser + real providers in Phases 2-3.
#pragma warning disable SA1402, SA1649, CA1062, CA1307
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.ELearning.Spike;

/// <summary>Spike helpers.</summary>
internal static partial class SpikeRules
{
    internal static readonly string[] VideoExtensions = [".mp4", ".mkv", ".webm", ".m4v", ".mov", ".avi"];

    [GeneratedRegex(@"^\s*(\d+)\s*(?:[.\-_)]\s*|\s+)")]
    internal static partial Regex Prefix();

    internal static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    internal static (long Number, string Title) Split(string name)
    {
        var m = Prefix().Match(name);
        return m.Success ? (long.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), name[m.Length..]) : (long.MaxValue, name);
    }

    internal static string Title(string name)
    {
        var t = Split(name).Title.Trim().TrimEnd('.').Trim();
        if (!t.Contains(' ', StringComparison.Ordinal))
        {
            t = t.Replace('_', ' ').Replace('-', ' ');
        }

        return t;
    }

    internal static int Position(string path, string[] siblings)
        => Array.IndexOf(siblings.OrderBy(p => Split(Path.GetFileName(p)).Number).ThenBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToArray(), path) + 1;

    internal static string[] SectionDirs(string seriesPath)
        => Directory.GetDirectories(seriesPath).Where(d => Directory.EnumerateFiles(d).Any(IsVideo)).ToArray();
}

/// <summary>Spike: series name.</summary>
public class SpikeSeriesProvider : ICustomMetadataProvider<Series>
{
    /// <inheritdoc />
    public string Name => "E-Learning spike";

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Series item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        item.Name = Path.GetFileName(item.Path) + " (spike)";
        return Task.FromResult(ItemUpdateType.MetadataEdit);
    }
}

/// <summary>Spike: season number = position among section folders.</summary>
public class SpikeSeasonProvider : ICustomMetadataProvider<Season>
{
    /// <inheritdoc />
    public string Name => "E-Learning spike";

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Season item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (item.IsVirtualItem || string.IsNullOrEmpty(item.Path) || item.Series?.Path is not { } seriesPath)
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        item.IndexNumber = SpikeRules.Position(item.Path, SpikeRules.SectionDirs(seriesPath));
        item.Name = SpikeRules.Title(Path.GetFileName(item.Path));
        return Task.FromResult(ItemUpdateType.MetadataEdit);
    }
}

/// <summary>Spike: episode number = position within its section folder.</summary>
public class SpikeEpisodeProvider : ICustomMetadataProvider<Episode>
{
    /// <inheritdoc />
    public string Name => "E-Learning spike";

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Episode item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (item.Series?.Path is not { } seriesPath || Path.GetDirectoryName(item.Path) is not { } dir)
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        item.ParentIndexNumber = dir == seriesPath ? 1 : SpikeRules.Position(dir, SpikeRules.SectionDirs(seriesPath));
        item.IndexNumber = SpikeRules.Position(item.Path, Directory.GetFiles(dir).Where(SpikeRules.IsVideo).ToArray());
        item.Name = SpikeRules.Title(Path.GetFileNameWithoutExtension(item.Path));
        return Task.FromResult(ItemUpdateType.MetadataEdit);
    }
}

/// <summary>Spike: every video-containing folder directly under a course is a Season.</summary>
public class SpikeSeasonResolver : MediaBrowser.Controller.Resolvers.IItemResolver
{
    /// <inheritdoc />
    public MediaBrowser.Controller.Resolvers.ResolverPriority Priority => MediaBrowser.Controller.Resolvers.ResolverPriority.Plugin;

    /// <inheritdoc />
    public MediaBrowser.Controller.Entities.BaseItem? ResolvePath(ItemResolveArgs args)
    {
        if (args.Parent is not Series series || !args.IsDirectory || !Directory.EnumerateFiles(args.Path).Any(SpikeRules.IsVideo))
        {
            return null;
        }

        return new Season { Path = args.Path, SeriesId = series.Id, SeriesName = series.Name, Name = Path.GetFileName(args.Path) };
    }
}
