using Jellyfin.Plugin.ELearning.Api;
using Jellyfin.Plugin.ELearning.Courses;
using Jellyfin.Plugin.ELearning.Progress;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ELearning.Tests;

public sealed class CourseOverridesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("elearning-tests").FullName;

    private static readonly string[] _files =
    [
        "01 Basics/01 Intro.mp4",
        "01 Basics/02 Setup.mp4",
        "02 Advanced/03 Deep dive.mp4",
        "notes.pdf",
    ];

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Without_overrides_nothing_changes()
    {
        var plain = CourseParser.Parse("C", _files);
        var same = CourseParser.Parse("C", _files, null, new CourseOverrides());

        Assert.Equal(OutlineRenderer.Render(plain), OutlineRenderer.Render(same));
        Assert.Null(plain.Description);
    }

    [Fact]
    public void Titles_and_descriptions_replace_the_parsed_ones()
    {
        var overrides = CourseOverrides.TryParse("""
            {
              "title": "My Course", "description": "About it.", "studio": "Me", "year": 2020,
              "sections": { "02 Advanced": { "title": "Harder Things", "description": "Section text." } },
              "lectures": { "01 Basics/02 Setup.mp4": { "title": "Getting set up", "description": "Lecture text." } }
            }
            """);

        var course = CourseParser.Parse("C", _files, null, overrides);

        Assert.Equal(("My Course", "Me", 2020, "About it."), (course.Title, course.Studio, course.Year, course.Description));
        Assert.Equal("Harder Things", course.Sections[1].Title);
        Assert.Equal("Section text.", course.Sections[1].Description);
        Assert.Equal("Getting set up", course.Sections[0].Lectures[1].Title);
        Assert.Equal("Lecture text.", course.Sections[0].Lectures[1].Description);
        Assert.Equal("Intro", course.Sections[0].Lectures[0].Title);
    }

    [Fact]
    public void Order_changes_sections_lectures_and_their_season_and_episode_numbers()
    {
        var overrides = CourseOverrides.TryParse("""
            {
              "sections": { "01 Basics": { "order": 5 } },
              "lectures": { "01 Basics/01 Intro.mp4": { "order": 10 } }
            }
            """);

        var course = CourseParser.Parse("C", _files, null, overrides);

        Assert.Equal(["Advanced", "Basics"], course.Sections.Select(s => s.Title));
        Assert.Equal([1, 2], course.Sections.Select(s => s.SeasonNumber!.Value));
        Assert.Equal(["Setup", "Intro"], course.Sections[1].Lectures.Select(l => l.Title));
        Assert.Equal([1, 2], course.Sections[1].Lectures.Select(l => l.EpisodeNumber!.Value));
        Assert.Equal(1, course.Sections[1].Lectures[1].Number);
    }

    [Fact]
    public void Fractions_slot_between_numbers_and_unlisted_items_keep_theirs()
    {
        var overrides = CourseOverrides.TryParse("""{ "lectures": { "01 Basics/02 Setup.mp4": { "order": 0.5 } } }""");

        var course = CourseParser.Parse("C", _files, null, overrides);

        Assert.Equal(["Setup", "Intro"], course.Sections[0].Lectures.Select(l => l.Title));
    }

    [Fact]
    public void Keys_accept_backslashes_and_dot_for_the_implicit_section()
    {
        var overrides = CourseOverrides.TryParse("""
            {
              "sections": { ".": { "title": "Everything" } },
              "lectures": { "1 b.mp4": { "title": "Second" }, "\\2 a.mp4": { "title": "Not matched" } }
            }
            """);

        var course = CourseParser.Parse("C", ["2 a.mp4", "1 b.mp4"], null, overrides);

        Assert.Equal("Everything", Assert.Single(course.Sections).Title);
        Assert.Equal(["Second", "Not matched"], course.Sections[0].Lectures.Select(l => l.Title));
    }

    [Fact]
    public void Blank_values_are_ignored()
    {
        var overrides = CourseOverrides.TryParse("""{ "title": "  ", "description": "", "sections": { "01 Basics": { "title": "" } } }""");

        var course = CourseParser.Parse("My Course", _files, null, overrides);

        Assert.Equal("My Course", course.Title);
        Assert.Null(course.Description);
        Assert.Equal("Basics", course.Sections[0].Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "year": "soon" }""")]
    public void Invalid_files_are_rejected(string json)
        => Assert.Null(CourseOverrides.TryParse(json));

    [Fact]
    public void Comments_and_trailing_commas_are_allowed()
    {
        var overrides = CourseOverrides.TryParse("""
            {
              // why
              "TITLE": "Loose",
            }
            """);

        Assert.Equal("Loose", overrides!.Title);
    }

    [Fact]
    public void Overrides_never_change_file_ids()
    {
        var listing = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Listings", "Dometrain - Getting Started - Onabaru in .MOM.txt")).Where(l => l.Length > 0).ToList();
        var first = listing.First(l => l.EndsWith(".mp4", StringComparison.Ordinal));
        var overrides = CourseOverrides.TryParse($$"""{ "title": "X", "lectures": { "{{first}}": { "title": "Y", "order": 99 } } }""");

        var plain = CourseParser.Parse("C", listing, "scope");
        var changed = CourseParser.Parse("C", listing, "scope", overrides);

        Assert.Equal(
            plain.Sections.SelectMany(s => s.Lectures).Select(l => (l.RelativePath, l.Id)).Order().ToList(),
            changed.Sections.SelectMany(s => s.Lectures).Select(l => (l.RelativePath, l.Id)).Order().ToList());
    }

    [Fact]
    public void Index_keeps_overridden_values_after_reload()
    {
        var course = Path.Combine(_dir, "library", "Some Course");
        Directory.CreateDirectory(Path.Combine(course, "01 A"));
        File.WriteAllText(Path.Combine(course, "01 A", "1 a.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(course, "course.json"), """
            { "title": "Renamed", "description": "Course text.",
              "sections": { "01 A": { "title": "First", "description": "Section text." } },
              "lectures": { "01 A/1 a.mp4": { "title": "Hello", "description": "Lecture text." } } }
            """);
        var file = Path.Combine(_dir, "data", "course-index.json");
        new CourseIndex(file, NullLogger<CourseIndex>.Instance).Replace([CourseScanner.Scan(course)]);

        var loaded = Assert.Single(new CourseIndex(file, NullLogger<CourseIndex>.Instance).Courses).Course;

        Assert.Equal(("Renamed", "Course text."), (loaded.Title, loaded.Description));
        Assert.Equal(("First", "Section text."), (loaded.Sections[0].Title, loaded.Sections[0].Description));
        Assert.Equal(("Hello", "Lecture text."), (loaded.Sections[0].Lectures[0].Title, loaded.Sections[0].Lectures[0].Description));
    }

    [Fact]
    public void Outline_shows_overridden_titles_and_order()
    {
        var overrides = CourseOverrides.TryParse("""
            { "title": "My Course",
              "sections": { "02 Advanced": { "order": 0 } },
              "lectures": { "01 Basics/01 Intro.mp4": { "title": "Welcome" } } }
            """);
        var course = CourseParser.Parse("C", _files, null, overrides);

        var outline = OutlineBuilder.Build(Guid.Empty, course, new Dictionary<string, EpisodeState>(), new Dictionary<int, Guid>(), new HashSet<string>(), (_, _) => new FileFacts(1, null));

        Assert.Equal("My Course", outline.Title);
        Assert.Equal(["02 Advanced", "01 Basics"], outline.Sections.Select(s => s.Title is "Advanced" ? "02 Advanced" : "01 Basics"));
        Assert.Equal(["Setup", "Welcome"], outline.Sections[1].Lectures.Select(l => l.Title).Order());
    }

    [Fact]
    public void Scanner_reads_course_json_and_does_not_list_it_as_a_resource()
    {
        var course = Path.Combine(_dir, "Some Course");
        Directory.CreateDirectory(course);
        File.WriteAllText(Path.Combine(course, "1 a.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(course, "course.json"), """{ "title": "Renamed" }""");

        var indexed = CourseScanner.Scan(course).Course;

        Assert.Equal("Renamed", indexed.Title);
        Assert.Empty(indexed.Resources);
    }

    [Fact]
    public void Scanner_ignores_a_broken_course_json()
    {
        var course = Path.Combine(_dir, "Some Course");
        Directory.CreateDirectory(course);
        File.WriteAllText(Path.Combine(course, "1 a.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(course, "course.json"), "{ nope");

        var indexed = CourseScanner.Scan(course).Course;

        Assert.Equal("Some Course", indexed.Title);
        Assert.Empty(indexed.Resources);
    }
}
