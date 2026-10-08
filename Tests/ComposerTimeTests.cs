using Avalonia.Headless.XUnit;
using SignalScheduler.Presentation;
using SignalScheduler.ViewModels;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class ComposerTimeTests
{
    [AvaloniaFact]
    public void KeyboardInputKeepsDraftAndSeparatesDateAndTimeErrors()
    {
        var model = new MainWindowViewModel();
        var future = DateTime.Today.AddDays(2);
        model.DateInput = future.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
        model.TimeInput = "14:45";
        Assert.Equal(future.ToString("yyyy-MM-dd") + " 14:45", model.When);
        Assert.False(model.HasSendTimeError);
        model.DateInput = "31.02.2027";
        Assert.Equal("31.02.2027", model.DateInput);
        Assert.True(model.HasDateInputError);
        Assert.False(model.HasTimeInputError);
        Assert.False(model.CanSchedule);
        model.DateInput = future.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
        model.TimeInput = "25:80";
        Assert.False(model.HasDateInputError);
        Assert.True(model.HasTimeInputError);
        Assert.Contains("HH:mm", model.SendTimeError);
        model.TimeInput = "09:30";
        Assert.False(model.HasSendTimeError);
        model.DateInput = DateTime.Today.AddDays(-1).ToString("dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
        Assert.True(model.HasDateInputError);
        Assert.False(model.HasTimeInputError);
        model.TryClose();
    }

    [AvaloniaTheory]
    [InlineData(3, 8, 2, 30, "skipped")]
    [InlineData(11, 1, 1, 30, "twice")]
    public void PickerWallClockValuesStillRejectDstGapsAndOverlaps(int month, int day, int hour, int minute, string hint)
    {
        var model = new MainWindowViewModel();
        model.SelectedDate = new DateTime(2026, month, day);
        model.SelectedTime = new TimeSpan(hour, minute, 0);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Assert.False(SendTimeValidation.TryGetDue(model.When,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), zone, out _, out var error));
        Assert.Contains(hint, error);
        model.TryClose();
    }

    [AvaloniaFact]
    public void ReuseUpdatesBothPickersAndPreservesMultilineText()
    {
        var model = new MainWindowViewModel();
        model.CopyToComposer(new SignalScheduler.Models.ScheduledMessage(Guid.NewGuid(), "+12025550123",
            "First line\nSecond line", DateTimeOffset.Now.AddDays(-1), SignalScheduler.Models.MessageStatus.Sent,
            "+12025550100", "fixture-cli"));
        Assert.Equal("First line\nSecond line", model.Body);
        Assert.NotNull(model.SelectedDate);
        Assert.NotNull(model.SelectedTime);
        Assert.Equal(model.SelectedDate.Value.Date.Add(model.SelectedTime.Value).ToString("yyyy-MM-dd HH:mm"), model.When);
        Assert.False(model.HasSendTimeError);
        model.TryClose();
    }
}
