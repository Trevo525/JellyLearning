using Jellyfin.Plugin.ELearning.Progress;

namespace Jellyfin.Plugin.ELearning.Tests;

public class CompletionRuleTests
{
    private const long Second = TimeSpan.TicksPerSecond;

    [Theory]
    // 3-minute lecture (Jellyfin would mark it played at 5 % because it is shorter than 5 minutes).
    [InlineData(10, 180, false, false, 10)]
    [InlineData(90, 180, false, false, 90)]
    [InlineData(169, 180, false, false, 169)]
    [InlineData(170, 180, false, true, 0)] // last 10 s
    [InlineData(180, 180, false, true, 0)]
    // 1-hour lecture: 98 % (58:48) comes before the last 10 s.
    [InlineData(3240, 3600, false, false, 3240)] // 90 %: Jellyfin's default would complete it
    [InlineData(3528, 3600, false, true, 0)]
    // Start of a lecture: not worth resuming.
    [InlineData(4, 180, false, false, 0)]
    [InlineData(5, 180, false, false, 5)]
    // Rewatching a completed lecture keeps it complete but remembers the position.
    [InlineData(60, 180, true, true, 60)]
    public void Decides_completion_and_resume_position(int position, int runtime, bool wasPlayed, bool played, int resume)
        => Assert.Equal((played, resume * Second), CompletionRule.Evaluate(position * Second, runtime * Second, wasPlayed, 10, 98));

    [Theory]
    [InlineData(null, 180L)]
    [InlineData(10L, null)]
    [InlineData(10L, 0L)]
    [InlineData(-1L, 180L)]
    public void Leaves_unknown_positions_to_Jellyfin(long? position, long? runtime)
        => Assert.Null(CompletionRule.Evaluate(position * Second, runtime * Second, false, 10, 98));

    [Fact]
    public void Percent_zero_disables_the_percentage_rule()
        => Assert.Equal((false, 3528 * Second), CompletionRule.Evaluate(3528 * Second, 3600 * Second, false, 10, 0));

    [Fact]
    public void Exact_position_is_kept_to_the_tick()
        => Assert.Equal((false, 1234567890L), CompletionRule.Evaluate(1234567890L, 3600 * Second, false, 10, 98));
}
