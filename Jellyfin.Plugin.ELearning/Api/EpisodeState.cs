using System;

namespace Jellyfin.Plugin.ELearning.Api;

/// <summary>
/// A video lecture's Jellyfin episode and the user's play state for it.
/// </summary>
/// <param name="ItemId">The episode ID.</param>
/// <param name="Played">Whether the user has played it.</param>
/// <param name="PositionTicks">Resume position.</param>
/// <param name="RunTimeTicks">Duration, if probed.</param>
public sealed record EpisodeState(Guid ItemId, bool Played, long PositionTicks, long? RunTimeTicks);
