namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// Overrides for one section or lecture.
/// </summary>
public sealed class ItemOverride
{
    /// <summary>Gets the title, used as written.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the sort key: items are ordered by this where they would use their file-name number.</summary>
    public double? Order { get; init; }
}
