using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// Pure naming rules for course folders and files (see PLAN.md, "Parsing rules").
/// </summary>
public static partial class CourseNaming
{
    private static readonly FrozenDictionary<string, FileKind> _kindsByExtension = new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = FileKind.Video,
        [".mkv"] = FileKind.Video,
        [".webm"] = FileKind.Video,
        [".m4v"] = FileKind.Video,
        [".mov"] = FileKind.Video,
        [".avi"] = FileKind.Video,
        [".srt"] = FileKind.Subtitle,
        [".vtt"] = FileKind.Subtitle,
        [".ass"] = FileKind.Subtitle,
        [".ssa"] = FileKind.Subtitle,
        [".pdf"] = FileKind.Pdf,
        [".html"] = FileKind.Html,
        [".htm"] = FileKind.Html,
        [".md"] = FileKind.Markdown,
        [".markdown"] = FileKind.Markdown,
        [".txt"] = FileKind.Text,
        [".jpg"] = FileKind.Image,
        [".jpeg"] = FileKind.Image,
        [".png"] = FileKind.Image,
        [".gif"] = FileKind.Image,
        [".webp"] = FileKind.Image,
        [".svg"] = FileKind.Image,
        [".zip"] = FileKind.Archive,
        [".7z"] = FileKind.Archive,
        [".tar"] = FileKind.Archive,
        [".gz"] = FileKind.Archive,
        [".tgz"] = FileKind.Archive,
        [".xlsx"] = FileKind.Document,
        [".xls"] = FileKind.Document,
        [".csv"] = FileKind.Document,
        [".docx"] = FileKind.Document,
        [".doc"] = FileKind.Document,
        [".pptx"] = FileKind.Document,
        [".ppt"] = FileKind.Document,
        [".odt"] = FileKind.Document,
        [".ods"] = FileKind.Document,
        [".odp"] = FileKind.Document,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Known course providers: folder-name token -> display name (Studio).
    private static readonly FrozenDictionary<string, string> _providers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Udemy"] = "Udemy",
        ["Skillshare"] = "Skillshare",
        ["Dometrain"] = "Dometrain",
        ["Craftsy"] = "Craftsy",
        ["Bluprint"] = "Bluprint",
        ["Coursera"] = "Coursera",
        ["Pluralsight"] = "Pluralsight",
        ["LinkedIn"] = "LinkedIn Learning",
        ["LinkedIn Learning"] = "LinkedIn Learning",
        ["Lynda"] = "Lynda",
        ["Domestika"] = "Domestika",
        ["MasterClass"] = "MasterClass",
        ["Packt"] = "Packt",
        ["OReilly"] = "O'Reilly",
        ["O'Reilly"] = "O'Reilly",
        ["Egghead"] = "egghead",
        ["FrontendMasters"] = "Frontend Masters",
        ["Frontend Masters"] = "Frontend Masters",
        ["Udacity"] = "Udacity",
        ["edX"] = "edX",
        ["CBT Nuggets"] = "CBT Nuggets",
        ["CBTNuggets"] = "CBT Nuggets",
        ["Wondrium"] = "Wondrium",
        ["CreativeLive"] = "CreativeLive",
        ["ZeroToMastery"] = "Zero To Mastery",
        ["Zero To Mastery"] = "Zero To Mastery",
        ["Educative"] = "Educative",
        ["DataCamp"] = "DataCamp",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> _junkExtensions = new[] { ".rar", ".sfv", ".nfo", ".par2", ".nzb", ".torrent", ".url", ".lnk", ".part" }
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> _junkNames = new[] { "Thumbs.db", "desktop.ini" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> _artworkNames = new[] { "poster", "cover", "folder" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the kind of a file from its extension.
    /// </summary>
    /// <param name="path">File name or path.</param>
    /// <returns>The kind.</returns>
    public static FileKind GetKind(string path)
        => _kindsByExtension.TryGetValue(Path.GetExtension(path), out var kind) ? kind : FileKind.Other;

    /// <summary>
    /// Gets a value indicating whether a file kind can be a lecture of its own in the outline.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>True for videos and readable documents.</returns>
    public static bool IsLectureKind(FileKind kind)
        => kind is FileKind.Video or FileKind.Html or FileKind.Markdown or FileKind.Text or FileKind.Pdf;

    /// <summary>
    /// Gets a value indicating whether a file is release/OS junk that is hidden everywhere.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>True when the file should be ignored.</returns>
    public static bool IsJunk(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return fileName.StartsWith('.')
            || _junkNames.Contains(fileName)
            || _junkExtensions.Contains(Path.GetExtension(fileName))
            || SplitRarVolume().IsMatch(fileName);
    }

    /// <summary>
    /// Gets a value indicating whether a course-root file is the course cover image.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>True for poster/cover/folder images.</returns>
    public static bool IsArtwork(string fileName)
        => GetKind(fileName) == FileKind.Image && _artworkNames.Contains(Path.GetFileNameWithoutExtension(fileName));

    /// <summary>
    /// Splits a numeric order prefix off a name ("04. What is onabaru" → 4, "What is onabaru").
    /// </summary>
    /// <param name="name">File name without extension, or folder name.</param>
    /// <returns>The number (null when there is none) and the remaining title.</returns>
    public static (int? Number, string Remainder) SplitNumber(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var match = NumberPrefix().Match(name);
        if (match.Success
            && match.Length < name.Length
            && int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return (number, name[match.Length..]);
        }

        return (null, name);
    }

    /// <summary>
    /// Turns a file/folder name (without extension) into a display title: drops the number prefix,
    /// turns slugs into words, trims trailing dots and collapses whitespace.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The title.</returns>
    public static string CleanTitle(string name)
    {
        var title = Humanize(SplitNumber(name).Remainder);
        return title.Length > 0 ? title : name.Trim();
    }

    /// <summary>
    /// Turns slugs into words ("anna-s-method" → "Anna's Method", "File_Handling" → "File Handling",
    /// "Working.Around" → "Working Around"). Names that already contain spaces keep their punctuation
    /// ("Free-Form").
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The humanized text.</returns>
    public static string Humanize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = Whitespace().Replace(text, " ").Trim();
        if (!result.Contains(' ', StringComparison.Ordinal))
        {
            result = Contractions(result).Replace('_', ' ');
            // Dots are the word gaps only when they outnumber dashes: "Solution.-.Center.the.clay", but
            // "What-is-.NET-Nebedo". ".." starts a dotfile ("Edit..zshenv" → "Edit .zshenv").
            if (!result.Contains(' ', StringComparison.Ordinal) && WordDot().Count(result) > result.Count(c => c == '-'))
            {
                result = WordDot().Replace(result.Replace("..", " .", StringComparison.Ordinal), " ");
            }

            if (!result.Contains(' ', StringComparison.Ordinal))
            {
                result = result.Replace('-', ' ');
            }

            result = Whitespace().Replace(result, " ").Trim();
            if (result.Any(char.IsLetter) && !result.Any(char.IsUpper))
            {
                result = TitleCase(result);
            }
        }

        return result.TrimEnd('.', ' ');
    }

    /// <summary>
    /// Builds the display name of a file: cleaned title plus a single extension
    /// ("egubak Lusu Pabi .pdf.pdf" → "egubak Lusu Pabi.pdf"). Files are never renamed.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The display name.</returns>
    public static string DisplayName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName).TrimEnd();
        while (extension.Length > 0 && stem.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^extension.Length].TrimEnd();
        }

        return CleanTitle(stem) + extension;
    }

    /// <summary>
    /// Parses a course folder name into title, studio and year.
    /// </summary>
    /// <param name="folderName">The course folder name.</param>
    /// <returns>Title, studio (if a known provider) and year (scene-style names).</returns>
    public static (string Title, string? Studio, int? Year) ParseCourseName(string folderName)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        var name = folderName.Trim();
        int? year = null;

        // Scene style: Provider.Title.Words.2026.BOOKWARE-GROUP — no spaces, dots between words.
        if (!name.Contains(' ', StringComparison.Ordinal) && name.Contains('.', StringComparison.Ordinal))
        {
            var scene = SceneSuffix().Match(name);
            if (scene.Success)
            {
                year = int.Parse(scene.Groups["year"].ValueSpan, CultureInfo.InvariantCulture);
                name = name[..scene.Index];
            }

            name = WordDot().Replace(Contractions(name), " ");
        }

        name = Humanize(name);

        string? studio = null;
        var dash = name.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0 && _providers.TryGetValue(name[..dash], out var provider))
        {
            studio = provider;
            name = name[(dash + 3)..].Trim();
        }
        else
        {
            var space = name.IndexOf(' ', StringComparison.Ordinal);
            if (space > 0 && year is not null && _providers.TryGetValue(name[..space], out provider))
            {
                studio = provider;
                name = name[(space + 1)..].Trim();
            }
        }

        return (name, studio, year);
    }

    /// <summary>
    /// Gets the language tag of a sidecar subtitle relative to its video
    /// ("003 Introduction_en.srt" for "003 Introduction" → "en").
    /// </summary>
    /// <param name="videoStem">Video file name without extension.</param>
    /// <param name="subtitleFileName">Subtitle file name.</param>
    /// <param name="language">The language tag, or null when the file name has none.</param>
    /// <returns>True when the subtitle belongs to the video.</returns>
    public static bool TryMatchSubtitle(string videoStem, string subtitleFileName, out string? language)
    {
        ArgumentNullException.ThrowIfNull(videoStem);
        language = null;
        var stem = Path.GetFileNameWithoutExtension(subtitleFileName);
        if (string.Equals(stem, videoStem, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (stem.Length <= videoStem.Length + 1
            || !stem.StartsWith(videoStem, StringComparison.OrdinalIgnoreCase)
            || stem[videoStem.Length] is not ('.' or '_' or '-' or ' '))
        {
            return false;
        }

        var tag = stem[(videoStem.Length + 1)..].Split('.', 2)[0];
        if (!LanguageTag().IsMatch(tag))
        {
            return false;
        }

        language = tag;
        return true;
    }

    /// <summary>
    /// Gets the lecture number of a per-lecture resources folder ("2-Resources" → 2).
    /// </summary>
    /// <param name="folderName">The folder name.</param>
    /// <returns>The lecture number, or null when the folder is not a lecture resources folder.</returns>
    public static int? GetResourcesFolderNumber(string folderName)
    {
        var match = ResourcesFolder().Match(folderName);
        if (!match.Success)
        {
            return null;
        }

        var digits = match.Groups["a"].Success ? match.Groups["a"].ValueSpan : match.Groups["b"].ValueSpan;
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>
    /// Builds a stable ID from a scope (course path or folder name) and a path relative to the course.
    /// </summary>
    /// <param name="scope">The scope the ID is unique within.</param>
    /// <param name="relativePath">Path relative to the course folder.</param>
    /// <returns>A 24-character lower-case hex ID.</returns>
    public static string FileId(string scope, string relativePath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(scope + "\n" + relativePath));
#pragma warning disable CA1308 // IDs are lower-case by convention, not compared culture-sensitively.
        return Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
#pragma warning restore CA1308
    }

    // In slugs a separator stands for a space, so "Developer.s.Guide" and "Let_s_Start" lost an apostrophe.
    private static string Contractions(string slug) => Contraction().Replace(slug, m => "'" + m.Value[1..]);

    private static string TitleCase(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (i == 0 || chars[i - 1] is ' ' or '-')
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
            }
        }

        return new string(chars);
    }

    [GeneratedRegex(@"^\s*(\d{1,6})\s*(?:[.\-_)]\s*|\s+)")]
    private static partial Regex NumberPrefix();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?<=[A-Za-z])[._-](?:s|ll|ve|re)(?=[._-])|(?<=[A-Za-z]n)[._-]t(?=[._-])", RegexOptions.IgnoreCase)]
    private static partial Regex Contraction();

    // A dot between words; dots between digits ("3.0") and after a space (".zshenv") stay.
    [GeneratedRegex(@"(?<![\s\d])\.|(?<=\d)\.(?=\D)")]
    private static partial Regex WordDot();

    [GeneratedRegex(@"\.(?<year>(?:19|20)\d{2})\.[A-Za-z0-9.]+-[A-Za-z0-9]+$")]
    private static partial Regex SceneSuffix();

    [GeneratedRegex(@"\.r\d{2,3}$", RegexOptions.IgnoreCase)]
    private static partial Regex SplitRarVolume();

    [GeneratedRegex(@"^[A-Za-z]{2,3}(?:[-_][A-Za-z0-9]{2,4})?$|^(?:forced|sdh|cc)$", RegexOptions.IgnoreCase)]
    private static partial Regex LanguageTag();

    [GeneratedRegex(@"^\s*(?:(?<a>\d{1,6})\s*[-_. ]\s*Resources?|Resources?\s*[-_ ]?\s*(?<b>\d{1,6}))\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ResourcesFolder();
}
