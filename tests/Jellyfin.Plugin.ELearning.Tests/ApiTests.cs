using System.Text.Json;
using Jellyfin.Plugin.ELearning.Api;
using Jellyfin.Plugin.ELearning.Courses;
using Jellyfin.Plugin.ELearning.Progress;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ELearning.Tests;

public sealed class ApiTests : IDisposable
{
    private const string Udemy = "UDEMY.Amuzepir.IvatETA.Mastery.for.Ugovozuviz.Unufus.3D.Kozenopi.2026.BOOKWARE-UpAbE";
    private const string Slug = "ivateta-konegore-mipunavel-kilulani-kafupap";

    private static readonly string _listings = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Listings");
    private static readonly FileFacts _present = new(1, null);

    private readonly string _dir = Directory.CreateTempSubdirectory("elearning-api-tests").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Meta_refresh_stub_is_detected_from_the_real_udemy_shape()
    {
        const string Html = """
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <meta http-equiv="refresh" content="0; nos=https://dev.to/x/afut-in-ilab#:~:text=In%20Ilab&amp;b=1">
            <title>Null</title></head><body><a href="https://dev.to/x">Click here</a></body></html>
            """;
        Assert.Equal("https://dev.to/x/afut-in-ilab#:~:text=In%20Ilab&b=1", LinkStub.Detect(Html)!.OriginalString);
    }

    [Theory]
    [InlineData("<meta content='5;URL=\"http://example.com/a\"' http-equiv='Refresh'>", "http://example.com/a")]
    [InlineData("<META HTTP-EQUIV=refresh CONTENT=\"0;url=https://example.com\">", "https://example.com/")]
    public void Meta_refresh_variants_are_detected(string html, string expected)
        => Assert.Equal(expected, LinkStub.Detect(html)!.AbsoluteUri);

    [Theory]
    [InlineData("<html><asik><h1>Mefagitu Lokerut</h1><img src=\"https://img-c.examplecdn.com/x.jpg\"></asik></html>")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"30\">")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"0; url=javascript:alert(1)\">")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"0; url=/relative/page\">")]
    [InlineData("<meta name=\"description\" content=\"0; url=https://example.com\">")]
    public void Pages_that_are_not_external_redirects_are_not_stubs(string html)
        => Assert.Null(LinkStub.Detect(html));

    [Fact]
    public void Markdown_is_rendered_without_raw_html()
    {
        var page = Documents.Markdown("# Setup\n\n<script>alert(1)</script>\n\n*done* <img src=x onerror=alert(1)>", "Setup <1>");
        Assert.Contains("<h1 id=\"setup\">Setup</h1>", page, StringComparison.Ordinal);
        Assert.Contains("<em>done</em>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", page, StringComparison.Ordinal);
        Assert.Contains("<title>Setup &lt;1&gt;</title>", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_and_link_pages_escape_their_content()
    {
        Assert.Contains("<pre>a &lt;b&gt; &amp; c</pre>", Documents.Text("a <b> & c", "t"), StringComparison.Ordinal);
        var link = Documents.Link(new Uri("https://example.com/?a=1&b=\"2\""), "Docs");
        Assert.Contains("href=\"https://example.com/?a=1&amp;b=%222%22\"", link, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_policy_sandboxes_without_same_origin_or_network()
    {
        Assert.StartsWith("sandbox allow-scripts ", Documents.ScriptPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("allow-same-origin", Documents.ScriptPolicy, StringComparison.Ordinal);
        Assert.Contains("connect-src 'none'", Documents.ScriptPolicy, StringComparison.Ordinal);
        Assert.StartsWith("sandbox ", Documents.StaticPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("script-src", Documents.StaticPolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void Progress_store_keeps_completion_per_user_across_restarts()
    {
        var folder = Path.Combine(_dir, "progress");
        Guid alice = Guid.NewGuid(), bob = Guid.NewGuid();
        var store = new ProgressStore(folder, NullLogger<ProgressStore>.Instance);
        store.SetCompleted(alice, "quiz1", true);
        store.SetCompleted(alice, "quiz2", true);
        store.SetCompleted(alice, "quiz2", false);
        store.SetCompleted(bob, "quiz2", true);

        var reloaded = new ProgressStore(folder, NullLogger<ProgressStore>.Instance);
        Assert.Equal(["quiz1"], reloaded.GetCompleted(alice));
        Assert.Equal(["quiz2"], reloaded.GetCompleted(bob));
        Assert.Empty(reloaded.GetCompleted(Guid.NewGuid()));
    }

    [Fact]
    public void Corrupt_progress_file_is_moved_aside_not_overwritten()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "progress")).FullName;
        var user = Guid.NewGuid();
        var file = Path.Combine(folder, user.ToString("N") + ".json");
        File.WriteAllText(file, "{ broken");

        var store = new ProgressStore(folder, NullLogger<ProgressStore>.Instance);
        Assert.Empty(store.GetCompleted(user));
        store.SetCompleted(user, "quiz1", true);
        Assert.Equal("{ broken", File.ReadAllText(file + ".corrupt"));
        Assert.Equal(["quiz1"], new ProgressStore(folder, NullLogger<ProgressStore>.Instance).GetCompleted(user));
    }

    [Fact]
    public void Outline_combines_episodes_documents_and_resources()
    {
        var course = Parse(Udemy);
        var sections = course.Sections;
        var firstVideo = sections[0].Lectures.First(l => l.Kind == FileKind.Video);
        var quiz = sections.SelectMany(s => s.Lectures).First(l => l.Kind == FileKind.Html);
        var stub = sections.SelectMany(s => s.Lectures).SelectMany(l => l.Attachments).First();
        var seriesId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        var outline = OutlineBuilder.Build(
            seriesId,
            course,
            new Dictionary<string, EpisodeState> { [firstVideo.RelativePath] = new(episodeId, true, 0, 600_000_000) },
            new Dictionary<int, Guid> { [1] = seasonId },
            new HashSet<string> { ProgressStore.Key(course.FolderName, quiz.RelativePath) },
            (path, kind) => path == stub.RelativePath ? new FileFacts(900, new Uri("https://example.com/docs")) : _present);

        var total = sections.Sum(s => s.Lectures.Count);
        Assert.Equal(seriesId, outline.SeriesId);
        Assert.Equal("Amuzepir IvatETA Mastery for Ugovozuviz Unufus 3D Kozenopi", outline.Title);
        Assert.Equal(new(total, 2), outline.Progress);

        var first = outline.Sections[0];
        Assert.Equal(seasonId, first.SeasonId);
        var video = first.Lectures.Single(l => l.Id == firstVideo.Id);
        Assert.Equal(episodeId, video.ItemId);
        Assert.True(video.Completed);
        Assert.Equal(600_000_000, video.RunTimeTicks);

        var quizLecture = outline.Sections.SelectMany(s => s.Lectures).Single(l => l.Id == quiz.Id);
        Assert.True(quizLecture.Completed);
        Assert.Null(quizLecture.ItemId);

        var quizSection = outline.Sections.Single(s => s.Lectures.Any(l => l.Id == quiz.Id));
        Assert.Null(quizSection.SeasonId);
        Assert.Equal(1, quizSection.Progress.Completed);

        var stubFile = outline.Sections.SelectMany(s => s.Lectures).SelectMany(l => l.Attachments).Single(f => f.Id == stub.Id);
        Assert.Equal(new Uri("https://example.com/docs"), stubFile.ExternalUrl);
        Assert.Equal(900, stubFile.Size);
    }

    [Fact]
    public void Outline_without_jellyfin_items_or_progress_is_all_open()
    {
        var course = Parse(Slug);
        var outline = OutlineBuilder.Build(Guid.Empty, course, new Dictionary<string, EpisodeState>(), new Dictionary<int, Guid>(), new HashSet<string>(), (_, _) => _present);

        Assert.Equal(0, outline.Progress.Completed);
        Assert.Equal(0, outline.Progress.Percent);
        Assert.All(outline.Sections.SelectMany(s => s.Lectures), l => Assert.False(l.Completed));
        Assert.All(outline.Sections.SelectMany(s => s.Lectures), l => Assert.Null(l.ItemId));
        Assert.Equal(course.Sections.Count, outline.Sections.Count);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 1, 33)]
    [InlineData(3, 3, 100)]
    [InlineData(200, 199, 99)]
    public void Percent_rounds_down_so_100_means_done(int total, int completed, int percent)
        => Assert.Equal(percent, new Api.Models.ProgressTotals(total, completed).Percent);

    [Fact]
    public void Index_resolves_lectures_attachments_and_resources_with_their_kind()
    {
        var (index, coursePath, course) = WriteIndex(Udemy);
        var quiz = course.Sections.SelectMany(s => s.Lectures).First(l => l.Kind == FileKind.Html);
        var attachment = course.Sections.SelectMany(s => s.Lectures).SelectMany(l => l.Attachments).First();

        var lecture = index.GetFile(quiz.Id)!;
        Assert.Same(lecture.Course, index.Courses.Single());
        Assert.Equal(quiz.Id, lecture.Lecture!.Id);
        Assert.Equal(FileKind.Html, lecture.File.Kind);
        Assert.Equal(Path.Combine(coursePath, quiz.RelativePath), lecture.AbsolutePath);

        var file = index.GetFile(attachment.Id)!;
        Assert.Null(file.Lecture);
        Assert.Equal(attachment, file.File);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0123456789abcdef01234567")]
    [InlineData("../../../etc/passwd")]
    [InlineData("%2e%2e%2fetc%2fpasswd")]
    public void Unknown_file_ids_resolve_to_nothing(string id)
    {
        var (index, _, _) = WriteIndex(Udemy);
        Assert.Null(index.GetFile(id));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("01-Intro/../../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData(".")]
    public void Tampered_index_paths_outside_the_course_are_refused(string relativePath)
    {
        var coursePath = Path.Combine(_dir, "library", "Course");
        var resource = new CourseFile("aaaaaaaaaaaaaaaaaaaaaaaa", relativePath, "outside.txt", FileKind.Text);
        var file = Path.Combine(_dir, "index.json");
        File.WriteAllText(file, JsonSerializer.Serialize(new[]
        {
            new IndexedCourse(coursePath, new Course("Course", "Course", null, null, null, [], [resource])),
        }));

        Assert.Null(new CourseIndex(file, NullLogger<CourseIndex>.Instance).GetFile(resource.Id));
    }

    [Fact]
    public void Same_course_in_two_libraries_gets_distinct_file_ids_but_shared_progress_keys()
    {
        var one = Path.Combine(_dir, "a", "Course");
        var two = Path.Combine(_dir, "b", "Course");
        foreach (var root in new[] { one, two })
        {
            Directory.CreateDirectory(Path.Combine(root, "01 Intro"));
            File.WriteAllText(Path.Combine(root, "01 Intro", "01 Welcome.mp4"), root);
            File.WriteAllText(Path.Combine(root, "01 Intro", "02 Quiz.html"), root);
        }

        var index = new CourseIndex(Path.Combine(_dir, "index.json"), NullLogger<CourseIndex>.Instance);
        index.Replace([CourseScanner.Scan(one), CourseScanner.Scan(two)]);
        var quizzes = index.Courses.Select(c => c.Course.Sections[0].Lectures[1]).ToList();

        Assert.NotEqual(quizzes[0].Id, quizzes[1].Id);
        Assert.Equal([Path.Combine(one, "01 Intro", "02 Quiz.html"), Path.Combine(two, "01 Intro", "02 Quiz.html")], quizzes.Select(q => index.GetFile(q.Id)!.AbsolutePath).Order());
        Assert.Equal(ProgressStore.Key("Course", quizzes[0].RelativePath), ProgressStore.Key("Course", quizzes[1].RelativePath));
    }

    private static Course Parse(string name)
        => CourseParser.Parse(name, File.ReadAllLines(Path.Combine(_listings, name + ".txt")).Where(l => l.Length > 0));

    private (CourseIndex Index, string CoursePath, Course Course) WriteIndex(string name)
    {
        var coursePath = Path.Combine(_dir, "library", name);
        var course = Parse(name);
        var file = Path.Combine(_dir, name + ".json");
        new CourseIndex(file, NullLogger<CourseIndex>.Instance).Replace([new IndexedCourse(coursePath, course)]);
        return (new CourseIndex(file, NullLogger<CourseIndex>.Instance), coursePath, course);
    }
}
