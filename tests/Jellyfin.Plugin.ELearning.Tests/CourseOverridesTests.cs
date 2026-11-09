using Jellyfin.Plugin.ELearning.Courses;

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
