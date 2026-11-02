using System;

namespace Jellyfin.Plugin.ELearning.Progress;

/// <summary>
/// The course completion rule (PLAN.md, "Progress tracking"): a lecture is complete only near its
/// very end; otherwise the exact position is kept. Replaces Jellyfin's movie rules (MinResumePct,
/// MaxResumePct and MinResumeDurationSeconds, which marks every video shorter than 5 minutes as
/// played once 5 % have been watched).
/// </summary>
public static class CompletionRule
{
    /// <summary>Positions this close to the start are not worth resuming.</summary>
    public static readonly TimeSpan MinimumResumePosition = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Decides played state and resume position for a reported playback position.
    /// </summary>
    /// <param name="positionTicks">Reported position.</param>
    /// <param name="runtimeTicks">Lecture duration.</param>
    /// <param name="wasPlayed">Whether the lecture was already complete before this playback.</param>
    /// <param name="secondsFromEnd">Complete when stopped within this many seconds of the end.</param>
    /// <param name="percent">... or once this percentage has been reached.</param>
    /// <returns>The state to store, or null when the rule cannot decide (unknown position/runtime).</returns>
    public static (bool Played, long PositionTicks)? Evaluate(long? positionTicks, long? runtimeTicks, bool wasPlayed, int secondsFromEnd, int percent)
    {
        if (positionTicks is not { } position || runtimeTicks is not { } runtime || runtime <= 0 || position < 0)
        {
            return null;
        }

        var nearEnd = position >= runtime - (Math.Max(0, secondsFromEnd) * TimeSpan.TicksPerSecond);
        var farEnough = percent is > 0 and <= 100 && (decimal)position * 100 >= (decimal)runtime * percent;
        if (nearEnd || farEnough)
        {
            return (true, 0);
        }

        return (wasPlayed, position < MinimumResumePosition.Ticks ? 0 : position);
    }
}
