using System.Text;
using Jellyfin.Plugin.ELearning.Web;

namespace Jellyfin.Plugin.ELearning.Tests;

public class WebAssetsTests
{
    [Fact]
    public void Script_tag_goes_before_body_end_once()
    {
        var page = WebAssets.Inject("<html><head></head><body><div id=\"reactRoot\"></div></body></html>");
        Assert.EndsWith($"<script defer src=\"../ELearning/Web/elearning.js?v={WebAssets.Version}\"></script></body></html>", page, StringComparison.Ordinal);
        Assert.Equal(page, WebAssets.Inject(page));
    }

    [Fact]
    public void Callback_accepts_the_file_transformation_payload()
        => Assert.Contains("elearning.js", WebInjection.TransformIndex(new IndexTransformPayload { Contents = "<body></body>" }), StringComparison.Ordinal);

    [Theory]
    [InlineData("elearning.js", "viewshow")]
    [InlineData("player.js", "Sessions/Playing")]
    [InlineData("player.css", ".sidebar")]
    [InlineData("hls.light.min.js", "")]
    public void Files_are_embedded(string name, string content)
    {
        var file = WebAssets.Get(name);
        Assert.NotNull(file);
        Assert.Contains(content, Encoding.UTF8.GetString(file.Value.Content), StringComparison.Ordinal);
    }

    [Fact]
    public void Player_page_links_its_files_with_the_version()
    {
        var page = Encoding.UTF8.GetString(WebAssets.Get(WebAssets.PlayerPage)!.Value.Content);
        Assert.Contains($"player.js?v={WebAssets.Version}", page, StringComparison.Ordinal);
        Assert.DoesNotContain("{{version}}", page, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{12}$", WebAssets.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../Plugin.cs")]
    [InlineData("player.JS")]
    [InlineData("Jellyfin.Plugin.ELearning.Web.player.js")]
    public void Unknown_names_are_not_served(string name) => Assert.Null(WebAssets.Get(name));
}
