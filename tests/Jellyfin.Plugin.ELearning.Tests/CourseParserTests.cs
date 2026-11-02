using Jellyfin.Plugin.ELearning.Courses;

namespace Jellyfin.Plugin.ELearning.Tests;

public class CourseParserTests
{
    [Fact]
    public void Flat_course_gets_one_implicit_section_in_numeric_order()
    {
        var course = CourseParser.Parse("C", ["10 Ten.mp4", "2 Two.mp4", "1 One.mp4", "notes.pdf"]);

        var section = Assert.Single(course.Sections);
        Assert.Equal(CourseParser.ImplicitSectionTitle, section.Title);
        Assert.Equal(1, section.SeasonNumber);
        Assert.Equal(["One", "Two", "Ten"], section.Lectures.Select(l => l.Title));
        Assert.Equal([1, 2, 3], section.Lectures.Select(l => l.EpisodeNumber!.Value));
        Assert.Equal("notes.pdf", Assert.Single(course.Resources).RelativePath);
    }

    [Fact]
    public void Episode_numbers_restart_per_section_but_raw_numbers_are_kept()
    {
        var course = CourseParser.Parse("C", ["01. A/01. x.mp4", "01. A/02. y.mp4", "02. B/03. z.mp4"]);

        var lecture = course.Sections[1].Lectures[0];
        Assert.Equal(2, course.Sections[1].SeasonNumber);
        Assert.Equal(1, lecture.EpisodeNumber);
        Assert.Equal(3, lecture.Number);
    }

    [Fact]
    public void Document_only_numbered_folder_is_a_section_without_season()
    {
        var course = CourseParser.Parse("C", ["01-Intro/1-a.mkv", "02-QUIZ_1/1-a_-_Quiz.html", "03-Next/2-b.mkv"]);

        Assert.Equal([1, null, 2], course.Sections.Select(s => s.SeasonNumber));
        var quiz = Assert.Single(course.Sections[1].Lectures);
        Assert.Equal(FileKind.Html, quiz.Kind);
        Assert.Null(quiz.EpisodeNumber);
    }

    [Fact]
    public void Folders_without_videos_or_numbered_lectures_are_course_resources()
    {
        var course = CourseParser.Parse("C", ["1 a.mp4", "Course Resources/x.pdf", "Guidebook/000-guide.pdf", "code.zip"]);

        Assert.Single(course.Sections);
        Assert.Equal(["code.zip", "Course Resources/x.pdf", "Guidebook/000-guide.pdf"], course.Resources.Select(r => r.RelativePath));
    }

    [Fact]
    public void Numbered_documents_between_videos_are_lectures_in_order()
    {
        var course = CourseParser.Parse("C", ["S/001 Intro.mp4", "S/002 Read me.html", "S/003 Setup.mp4", "S/004 Notes.md"]);

        var lectures = course.Sections[0].Lectures;
        Assert.Equal(["Intro", "Read me", "Setup", "Notes"], lectures.Select(l => l.Title));
        Assert.Equal([1, null, 2, null], lectures.Select(l => l.EpisodeNumber));
    }

    [Fact]
    public void Matching_documents_and_files_attach_to_their_video()
    {
        var course = CourseParser.Parse("C", ["S/03 Setup.mp4", "S/03 Setup - slides.pdf", "S/03 exercise.zip", "S/03 Other topic.html"]);

        var lectures = course.Sections[0].Lectures;
        Assert.Equal(2, lectures.Count);
        Assert.Equal(["S/03 exercise.zip", "S/03 Setup - slides.pdf"], lectures[0].Attachments.Select(a => a.RelativePath));
        Assert.Equal("Other topic", lectures[1].Title);
    }

    [Fact]
    public void Lecture_resource_folders_attach_to_the_numbered_lecture()
    {
        var course = CourseParser.Parse("C", ["S/2-Install.mkv", "S/2-Resources/Download.html", "S/9-Resources/orphan.pdf", "S/Extras/a.txt"]);

        var section = course.Sections[0];
        Assert.Equal("S/2-Resources/Download.html", Assert.Single(section.Lectures[0].Attachments).RelativePath);
        Assert.Equal(["S/9-Resources/orphan.pdf", "S/Extras/a.txt"], section.Resources.Select(r => r.RelativePath));
    }

    [Fact]
    public void Subtitles_attach_to_the_longest_matching_video()
    {
        var course = CourseParser.Parse("C", ["S/003 Introduction.mp4", "S/003 Introduction to export.mp4", "S/003 Introduction to export_en.srt", "S/003 Introduction_en.vtt", "S/stray.srt"]);

        var lectures = course.Sections[0].Lectures;
        Assert.Equal("S/003 Introduction_en.vtt", Assert.Single(lectures.Single(l => l.Title == "Introduction").Subtitles).RelativePath);
        Assert.Equal("en", Assert.Single(lectures.Single(l => l.Title == "Introduction to export").Subtitles).Language);
        Assert.Equal("S/stray.srt", Assert.Single(course.Sections[0].Resources).RelativePath);
    }

    [Fact]
    public void Loose_videos_next_to_sections_form_a_first_implicit_section()
    {
        var course = CourseParser.Parse("C", ["0 Welcome.mp4", "01 - A/1 a.mp4"]);

        Assert.Equal([string.Empty, "01 - A"], course.Sections.Select(s => s.RelativePath));
        Assert.Equal([1, 2], course.Sections.Select(s => s.SeasonNumber));
    }

    [Fact]
    public void Junk_and_artwork_are_not_resources()
    {
        var course = CourseParser.Parse("C", ["1 a.mp4", "cover.jpg", "x.rar", "x.r00", "x.nfo", "x.sfv", ".DS_Store", "S/.hidden/y.pdf"]);

        Assert.Equal("cover.jpg", course.ArtworkPath);
        Assert.Empty(course.Resources);
        Assert.Single(course.Sections);
    }

    [Fact]
    public void Windows_separators_are_normalized()
    {
        var course = CourseParser.Parse("C", [@"01. A\01. x.mp4"]);
        Assert.Equal("01. A/01. x.mp4", course.Sections[0].Lectures[0].RelativePath);
    }
}
