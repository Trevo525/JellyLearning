using System.Runtime.CompilerServices;
using Jellyfin.Plugin.ELearning.Courses;

namespace Jellyfin.Plugin.ELearning.Tests;

/// <summary>
/// Pins the full parse result of every real export layout. Season/episode numbers become
/// Jellyfin user-data keys, so any change here can orphan users' watch progress: review diffs
/// carefully. Regenerate with UPDATE_SNAPSHOTS=1 dotnet test.
/// </summary>
public class OutlineSnapshotTests
{
    private static readonly string _listings = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Listings");

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(_listings, "*.txt").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Outline_matches_snapshot(string course)
    {
        var listing = File.ReadAllLines(Path.Combine(_listings, course + ".txt")).Where(l => l.Length > 0);
        var actual = OutlineRenderer.Render(CourseParser.Parse(course, listing));
        var snapshot = Path.Combine(SourceDir(), "Fixtures", "Snapshots", course + ".txt");

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(snapshot, actual);
            return;
        }

        Assert.True(File.Exists(snapshot), $"Missing snapshot {snapshot}; run with UPDATE_SNAPSHOTS=1.");
        Assert.Equal(File.ReadAllText(snapshot), actual);
    }

    private static string SourceDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
