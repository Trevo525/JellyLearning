using System.Text;
using Jellyfin.Plugin.ELearning.Web;

namespace Jellyfin.Plugin.ELearning.Tests;

public class WebBundleTests
{
    [Fact]
    public void Script_tag_goes_before_body_end_once()
    {
        var page = WebBundle.Inject("<html><head></head><body><div id=\"reactRoot\"></div></body></html>");
        Assert.EndsWith($"<script defer src=\"../ELearning/Web/elearning.js?v={WebBundle.Version}\"></script></body></html>", page, StringComparison.Ordinal);
        Assert.Equal(page, WebBundle.Inject(page));
    }

    [Fact]
    public void Callback_accepts_the_file_transformation_payload()
        => Assert.Contains("elearning.js", WebInjection.TransformIndex(new IndexTransformPayload { Contents = "<body></body>" }), StringComparison.Ordinal);

    [Fact]
    public void Bundle_is_embedded_and_versioned_by_content()
    {
        Assert.Contains("viewshow", Encoding.UTF8.GetString(WebBundle.Content.Span), StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{12}$", WebBundle.Version);
    }
}
