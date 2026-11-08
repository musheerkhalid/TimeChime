using TimeChime.Core.Scheduling;
using Xunit;

namespace TimeChime.Core.Tests;

public class ScheduleCalculatorTests
{
    private static readonly HashSet<DayOfWeek> Weekdays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
    ];

    [Fact]
    public void IsInActiveWindow_DaytimeWindow_InclusiveStartExclusiveEnd()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        // Monday 09:00:00 -> exactly on start -> active
        var t1 = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        Assert.True(ScheduleCalculator.IsInActiveWindow(t1, start, end, false, Weekdays));

        // Monday 16:59:59 -> within window -> active
        var t2 = new DateTimeOffset(2026, 11, 2, 16, 59, 59, TimeSpan.Zero);
        Assert.True(ScheduleCalculator.IsInActiveWindow(t2, start, end, false, Weekdays));

        // Monday 17:00:00 -> exactly on end -> NOT active (exclusive end)
        var t3 = new DateTimeOffset(2026, 11, 2, 17, 0, 0, TimeSpan.Zero);
        Assert.False(ScheduleCalculator.IsInActiveWindow(t3, start, end, false, Weekdays));

        // Monday 08:59:59 -> before start -> NOT active
        var t4 = new DateTimeOffset(2026, 11, 2, 8, 59, 59, TimeSpan.Zero);
        Assert.False(ScheduleCalculator.IsInActiveWindow(t4, start, end, false, Weekdays));
    }

    [Fact]
    public void IsInActiveWindow_OvernightWindow_StartDayAnchoring()
    {
        var start = new TimeOnly(22, 0);
        var end = new TimeOnly(4, 0);

        // Friday 2026-11-06 22:30:00 -> active (Friday is enabled)
        var friNight = new DateTimeOffset(2026, 11, 6, 22, 30, 0, TimeSpan.Zero);
        Assert.True(ScheduleCalculator.IsInActiveWindow(friNight, start, end, false, Weekdays));

        // Saturday 2026-11-07 02:15:00 -> anchored to Friday -> active even though Saturday is not enabled!
        var satEarlyMorning = new DateTimeOffset(2026, 11, 7, 2, 15, 0, TimeSpan.Zero);
        Assert.True(ScheduleCalculator.IsInActiveWindow(satEarlyMorning, start, end, false, Weekdays));

        // Saturday 2026-11-07 04:00:00 -> window closed at 04:00 -> NOT active
        var satPastWindow = new DateTimeOffset(2026, 11, 7, 4, 0, 0, TimeSpan.Zero);
        Assert.False(ScheduleCalculator.IsInActiveWindow(satPastWindow, start, end, false, Weekdays));

        // Sunday morning 02:00 -> anchored to Saturday (disabled) -> NOT active
        var sunEarlyMorning = new DateTimeOffset(2026, 11, 8, 2, 0, 0, TimeSpan.Zero);
        Assert.False(ScheduleCalculator.IsInActiveWindow(sunEarlyMorning, start, end, false, Weekdays));
    }

    [Fact]
    public void CalculateNextOccurrence_AlignsToIntervalBoundaries()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        // At 09:07, next 15-minute slot should be 09:15
        var current = new DateTimeOffset(2026, 11, 2, 9, 7, 23, TimeSpan.Zero);
        var next15 = ScheduleCalculator.CalculateNextOccurrence(current, 15, start, end, false, Weekdays);
        Assert.NotNull(next15);
        Assert.Equal(9, next15.Value.Hour);
        Assert.Equal(15, next15.Value.Minute);
        Assert.Equal(0, next15.Value.Second);

        // At 09:07, next 30-minute slot should be 09:30
        var next30 = ScheduleCalculator.CalculateNextOccurrence(current, 30, start, end, false, Weekdays);
        Assert.NotNull(next30);
        Assert.Equal(9, next30.Value.Hour);
        Assert.Equal(30, next30.Value.Minute);

        // At 09:07, next 60-minute slot should be 10:00
        var next60 = ScheduleCalculator.CalculateNextOccurrence(current, 60, start, end, false, Weekdays);
        Assert.NotNull(next60);
        Assert.Equal(10, next60.Value.Hour);
        Assert.Equal(0, next60.Value.Minute);
    }

    [Fact]
    public void Evaluate_DelayTolerance_WithinTwentySecondsTriggers_PastToleranceSkips()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        // 10:15:05 -> 5 seconds past boundary (within 20s tolerance) -> ShouldTriggerNow = true
        var tWithin = new DateTimeOffset(2026, 11, 2, 10, 15, 5, TimeSpan.Zero);
        var resWithin = ScheduleCalculator.Evaluate(tWithin, true, false, 15, start, end, false, Weekdays);
        Assert.True(resWithin.ShouldTriggerNow);
        Assert.Equal("Triggering announcement", resWithin.StatusDescription);

        // 10:15:25 -> 25 seconds past boundary (exceeded 20s tolerance, e.g. late resume from sleep) -> skips stale slot
        var tPast = new DateTimeOffset(2026, 11, 2, 10, 15, 25, TimeSpan.Zero);
        var resPast = ScheduleCalculator.Evaluate(tPast, true, false, 15, start, end, false, Weekdays);
        Assert.False(resPast.ShouldTriggerNow);
        Assert.Equal("Awaiting next boundary", resPast.StatusDescription);
    }

    [Fact]
    public void Evaluate_DuplicateSuppression_PreventsRepeatingSameSlot()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        var current = new DateTimeOffset(2026, 11, 2, 14, 0, 10, TimeSpan.Zero);
        var slotBoundary = new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.Zero);

        // First attempt -> triggers
        var res1 = ScheduleCalculator.Evaluate(current, true, false, 15, start, end, false, Weekdays, lastAnnouncedInstant: null);
        Assert.True(res1.ShouldTriggerNow);

        // Second attempt with lastAnnouncedInstant set to 14:00 -> duplicate suppressed
        var res2 = ScheduleCalculator.Evaluate(current, true, false, 15, start, end, false, Weekdays, lastAnnouncedInstant: slotBoundary);
        Assert.False(res2.ShouldTriggerNow);
        Assert.Equal("Slot already announced", res2.StatusDescription);
    }
}

