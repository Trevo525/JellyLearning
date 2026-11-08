using Jellyfin.Plugin.ELearning.Courses;

namespace Jellyfin.Plugin.ELearning.Tests;

public class CourseNamingTests
{
    [Theory]
    [InlineData("04. What is onabaru", 4, "What is onabaru")]
    [InlineData("1 Introduction", 1, "Introduction")]
    [InlineData(" 24 Peru Opalo 1", 24, "Peru Opalo 1")]
    [InlineData("01 - introduction", 1, "introduction")]
    [InlineData("001-sukuz_two-funi_erito", 1, "sukuz_two-funi_erito")]
    [InlineData("2-Resources", 2, "Resources")]
    [InlineData("3) Setup", 3, "Setup")]
    [InlineData("3D Kozenopi", null, "3D Kozenopi")]
    [InlineData("Course Resources", null, "Course Resources")]
    [InlineData("42", null, "42")]
    public void SplitNumber_parses_order_prefix(string name, int? number, string remainder)
        => Assert.Equal((number, remainder), CourseNaming.SplitNumber(name));

    [Theory]
    [InlineData("18 Are Efipofuve Rukoledu ubotu the epog.", "Are Efipofuve Rukoledu ubotu the epog")]
    [InlineData("4 Arape Of 3D Kozenopi", "Arape Of 3D Kozenopi")]
    [InlineData("02 - anna-s-method-for-drawing", "Anna's Method For Drawing")]
    [InlineData("03-Let_s_Start_Painting", "Let's Start Painting")]
    [InlineData("59.-What_s-a-Rain-Gauge", "What's a Rain Gauge")]
    [InlineData("12-We_ll_See_why_you_ve_got_this", "We'll See why you've got this")]
    [InlineData("4-don_t_panic", "Don't Panic")]
    [InlineData("5-Bits_t_and_s", "Bits t and s")]
    [InlineData("01.Clay.for.hand.building", "Clay for hand building")]
    [InlineData("03-Working.Around", "Working Around")]
    [InlineData("08-Step-By-Step.Clay", "Step By Step.Clay")]
    [InlineData("38.-What-is-.NET-Nebedo", "What is .NET Nebedo")]
    [InlineData("12.-Charts-with-Chart.js", "Charts with Chart.js")]
    [InlineData("7.-Garden.State", "Garden.State")]
    [InlineData("05.Challenge.-.Center.the.clay", "Challenge - Center the clay")]
    [InlineData("12.Solution.-.Edit..zshenv", "Solution - Edit .zshenv")]
    [InlineData("2.Upgrading.to.3.0", "Upgrading to 3.0")]
    [InlineData("3-ASP.NET_Core_basics", "ASP.NET Core basics")]
    [InlineData("005-ubut-dito_efogo_subaseruza", "Nebedo-Form Okarez Techniques")]
    [InlineData("7-Arranging_toolbars_-_Quiz", "Arranging toolbars - Quiz")]
    [InlineData("08-OKAREZ", "OKAREZ")]
    [InlineData("001 ATTENTION!  READ FIRST", "ATTENTION! READ FIRST")]
    [InlineData("80. Shortcuts... not", "Shortcuts... not")]
    [InlineData("12", "12")]
    public void CleanTitle_humanizes(string name, string expected)
        => Assert.Equal(expected, CourseNaming.CleanTitle(name));

    [Theory]
    [InlineData("egubak Lusu Pabi .pdf.pdf", "egubak Lusu Pabi.pdf")]
    [InlineData("Weekly Practice Planner..pdf", "Weekly Practice Planner.pdf")]
    [InlineData("Wagu.Kpart", "Wagu.Kpart")]
    [InlineData("000-course_guidebook_recipes.pdf", "Course Guidebook Recipes.pdf")]
    [InlineData("code.zip", "Code.zip")]
    public void DisplayName_cleans_file_names(string fileName, string expected)
        => Assert.Equal(expected, CourseNaming.DisplayName(fileName));

    [Theory]
    [InlineData("UDEMY.Ufibato.Usinavaki.Zimoruk.Ilab.2026.BOOKWARE-UpAbE", "Ufibato Usinavaki Zimoruk Ilab", "Udemy", 2026)]
    [InlineData("Craftsy.-.Idizel.Teta.Lune.Erito.And.Ovobiz.2025.BOOKWARE-ZEKAKISE", "Idizel Teta Lune Erito And Ovobiz", "Craftsy", 2025)]
    [InlineData("Dometrain - Getting Started - Onabaru in .MOM", "Getting Started - Onabaru in .MOM", "Dometrain", null)]
    [InlineData("Skillshare - How to Tivav", "How to Tivav", "Skillshare", null)]
    [InlineData("3D Kozenopi - A Amuzepir Course with Nebedo Babofap", "3D Kozenopi - A Amuzepir Course with Nebedo Babofap", null, null)]
    [InlineData("ivateta-konegore-mipunavel-kilulani-kafupap", "Ivateta Konegore Mipunavel Kilulani Kafupap", null, null)]
    [InlineData("Some.Course.Without.Scene.Tag", "Some Course Without Scene Tag", null, null)]
    [InlineData("UDEMY.Knitting.The.Complete.Beginner.s.Guide.2026.BOOKWARE-EXAMPLE", "Knitting The Complete Beginner's Guide", "Udemy", 2026)]
    [InlineData("Some.Course.Version.2.5.Upgrade", "Some Course Version 2.5 Upgrade", null, null)]
    [InlineData("Udemy Is Not A Studio Without Separator", "Udemy Is Not A Studio Without Separator", null, null)]
    public void ParseCourseName_extracts_title_studio_year(string folder, string title, string? studio, int? year)
        => Assert.Equal((title, studio, year), CourseNaming.ParseCourseName(folder));

    [Theory]
    [InlineData("003 Introduction", "003 Introduction_en.srt", true, "en")]
    [InlineData("003 Introduction", "003 Introduction.en.srt", true, "en")]
    [InlineData("003 Introduction", "003 Introduction.pt-BR.vtt", true, "pt-BR")]
    [InlineData("003 Introduction", "003 Introduction.srt", true, null)]
    [InlineData("003 Introduction", "003 Introduction.en.forced.srt", true, "en")]
    [InlineData("003 Introduction", "003 Introduction to file types_en.srt", false, null)]
    [InlineData("003 Introduction", "004 Installation_en.srt", false, null)]
    public void TryMatchSubtitle_matches_sidecars(string video, string subtitle, bool matches, string? language)
    {
        Assert.Equal(matches, CourseNaming.TryMatchSubtitle(video, subtitle, out var actual));
        Assert.Equal(language, actual);
    }

    [Theory]
    [InlineData("udemy.course.2026.bookware-upabe.gar", true)]
    [InlineData("udemy.course.2026.bookware-upabe.r07", true)]
    [InlineData("udemy.course.2026.bookware-upabe.vus", true)]
    [InlineData("release.nfo", true)]
    [InlineData(".DS_Store", true)]
    [InlineData("Thumbs.db", true)]
    [InlineData("004 notes4-draft.txt.part", true)]
    [InlineData("code.zip", false)]
    [InlineData("notes.r", false)]
    public void IsJunk_hides_release_and_os_files(string name, bool junk)
        => Assert.Equal(junk, CourseNaming.IsJunk(name));

    [Theory]
    [InlineData("2-Resources", 2)]
    [InlineData("125-Resources", 125)]
    [InlineData("03 - Resources", 3)]
    [InlineData("Resources 4", 4)]
    [InlineData("Course Resources", null)]
    [InlineData("02-QUIZ_1_IvatETA_Emememezigif", null)]
    public void GetResourcesFolderNumber_detects_lecture_resource_folders(string folder, int? number)
        => Assert.Equal(number, CourseNaming.GetResourcesFolderNumber(folder));

    [Fact]
    public void NaturalComparer_orders_numbers_by_value()
    {
        string[] names = ["10 Ninib Okarez Pt1", "2 About Me", "1 Introduction", "Lusu Pabi", "002 B", "1 Introduction"];
        Assert.Equal(["1 Introduction", "1 Introduction", "2 About Me", "002 B", "10 Ninib Okarez Pt1", "Lusu Pabi"], names.Order(NaturalComparer.Instance));
    }

    [Fact]
    public void FileId_is_stable_and_path_specific()
    {
        var id = CourseNaming.FileId("Course", "01/a.mp4");
        Assert.Equal(24, id.Length);
        Assert.Equal(id, CourseNaming.FileId("Course", "01/a.mp4"));
        Assert.NotEqual(id, CourseNaming.FileId("Course", "01/b.mp4"));
        Assert.NotEqual(id, CourseNaming.FileId("Other", "01/a.mp4"));
    }
}
