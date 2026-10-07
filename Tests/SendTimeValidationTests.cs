using SignalScheduler.Presentation;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class SendTimeValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 20, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-10-07 23:011")]
    [InlineData("2026-10-07 23:01x")]
    [InlineData("2026-10-07 23:01\n")]
    [InlineData("2026-10-07 23:01 ")]
    [InlineData("2026-10-07 23:60")]
    [InlineData("2026-10-07 24:01")]
    [InlineData("2026-02-30 23:01")]
    [InlineData("2026-1-07 23:01")]
    public void MalformedInputProducesExplicitError(string text)
    {
        Assert.False(SendTimeValidation.TryGetDue(text, Now, TimeZoneInfo.Utc, out _, out var error));
        Assert.Contains("yyyy-MM-dd HH:mm", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyInputAsksForTime(string? text)
    {
        Assert.False(SendTimeValidation.TryGetDue(text, Now, TimeZoneInfo.Utc, out _, out var error));
        Assert.Contains("Enter", error);
    }

    [Fact]
    public void FutureTimeIsAcceptedAndBecomesInvalidAtItsDueMinute()
    {
        Assert.True(SendTimeValidation.TryGetDue("2026-10-07 23:01", Now, TimeZoneInfo.Utc, out var due, out var error));
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 23, 1, 0, TimeSpan.Zero), due);
        Assert.Equal("", error);
        Assert.False(SendTimeValidation.TryGetDue("2026-10-07 23:01", due, TimeZoneInfo.Utc, out _, out error));
        Assert.Contains("future", error);
    }

    [Theory]
    [InlineData("2026-03-08 02:30", "skipped")]
    [InlineData("2026-11-01 01:30", "twice")]
    public void DaylightSavingGapsAndOverlapsRemainRejected(string text, string hint)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Assert.False(SendTimeValidation.TryGetDue(text, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), zone, out _, out var error));
        Assert.Contains(hint, error);
    }
}
