using DCenter.Server.Services;

namespace DCenter.Server.Tests;

public class ClockTests
{
    private sealed class FixedTime(DateTimeOffset utcNow, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public override TimeZoneInfo LocalTimeZone => zone;
    }

    private static readonly TimeZoneInfo PlusEight = TimeZoneInfo.CreateCustomTimeZone("UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");

    [Fact]
    public void LocalNow_IsWallClockTimeInTheProviderZone()
    {
        var time = new FixedTime(new DateTimeOffset(2026, 3, 15, 20, 30, 0, TimeSpan.Zero), PlusEight);

        var now = time.LocalNow();

        Assert.Equal(new DateTime(2026, 3, 16, 4, 30, 0), now);
        Assert.Equal(DateTimeKind.Local, now.Kind);
    }

    [Fact]
    public void Today_RollsOverAtLocalMidnight()
    {
        var beforeMidnight = new FixedTime(new DateTimeOffset(2026, 3, 15, 15, 59, 0, TimeSpan.Zero), PlusEight);
        var afterMidnight = new FixedTime(new DateTimeOffset(2026, 3, 15, 16, 1, 0, TimeSpan.Zero), PlusEight);

        Assert.Equal(new DateOnly(2026, 3, 15), beforeMidnight.Today());
        Assert.Equal(new DateOnly(2026, 3, 16), afterMidnight.Today());
    }

    [Fact]
    public void SystemClock_MatchesDateTimeNow()
    {
        var before = DateTime.Now;
        var now = TimeProvider.System.LocalNow();
        var after = DateTime.Now;

        Assert.InRange(now, before, after);
        Assert.Equal(DateTimeKind.Local, now.Kind);
    }
}
