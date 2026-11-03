namespace Jellyfin.Plugin.ELearning.Api.Models;

/// <summary>
/// Completion counts for a course or section.
/// </summary>
/// <param name="Total">Number of lectures.</param>
/// <param name="Completed">Number of completed lectures.</param>
public sealed record ProgressTotals(int Total, int Completed)
{
    /// <summary>Gets the completed share in whole percent (rounded down, so 100 means all done).</summary>
    public int Percent => Total == 0 ? 0 : Completed * 100 / Total;
}
