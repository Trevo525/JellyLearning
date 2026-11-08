using Jellyfin.Plugin.ELearning.Library;
using SkiaSharp;

namespace Jellyfin.Plugin.ELearning.Tests;

/// <summary>
/// Generated covers. Set COVER_PREVIEW_DIR to also write the samples there for a look.
/// </summary>
public class CoverRendererTests
{
    public static TheoryData<string, string?, string?> Samples => new()
    {
        { "Getting Started - Sourdough", null, "Dometrain" },
        { "Build A Backyard Greenhouse", null, "Udemy" },
        { "Skillshare How To Plan, Plant And Keep A Vegetable Garden Going All Year Long Even In A Small Space-EXAMPLE", null, null },
        { "Watercolour Landscapes: Absolute Beginners", null, null },
        { "Getting Started - Sourdough 2024-6", "Lectures", "Dometrain" },
        { "Mixing the Dough by Hand and Machine", "Section 2", "Bread Baking Become a Confident Home Baker Today" },
        { "Clay", "Section 12", "Linkedin Learning Learning Pottery-EXAMPLE" },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Render_draws_a_portrait_jpeg(string title, string? label, string? footer)
    {
        var jpeg = CoverRenderer.Render(title, label, footer);

        using var bitmap = SKBitmap.Decode(jpeg);
        Assert.Equal((CoverRenderer.Width, CoverRenderer.Height), (bitmap.Width, bitmap.Height));
        Preview(title + (label is null ? string.Empty : " - " + label), jpeg);
    }

    [Fact]
    public void Render_is_the_same_every_time()
        => Assert.Equal(CoverRenderer.Render("Knitting The Complete Beginner's Guide", null, "Udemy"), CoverRenderer.Render("Knitting The Complete Beginner's Guide", null, "Udemy"));

    [Fact]
    public void Wrap_keeps_lines_within_width()
    {
        using var font = new SKFont(SKTypeface.Default, 60);
        var lines = CoverRenderer.Wrap("Mastering Bread The Comprehensive Guide to the Home Kitchen", font, 400);

        Assert.True(lines.Count > 1);
        Assert.All(lines.Where(l => l.Contains(' ', StringComparison.Ordinal)), l => Assert.True(font.MeasureText(l) <= 400, l));
        Assert.Equal("Mastering Bread The Comprehensive Guide to the Home Kitchen", string.Join(' ', lines));
    }

    private static void Preview(string name, byte[] jpeg)
    {
        var folder = Environment.GetEnvironmentVariable("COVER_PREVIEW_DIR");
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, string.Concat(name.Split(Path.GetInvalidFileNameChars())) + ".jpg"), jpeg);
        }
    }
}
