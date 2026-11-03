namespace Jellyfin.Plugin.ELearning.Web;

/// <summary>
/// What File Transformation passes to <see cref="WebInjection.TransformIndex"/>.
/// </summary>
public sealed class IndexTransformPayload
{
    /// <summary>Gets or sets the file contents.</summary>
    public string? Contents { get; set; }
}
