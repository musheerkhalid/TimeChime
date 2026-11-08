using TimeChime.Core.Models;

namespace TimeChime.Core.Scheduling;

public sealed record ScheduleCalculationResult(
    DateTimeOffset? NextOccurrence,
    bool IsCurrentlyInActiveWindow,
    bool ShouldTriggerNow,
    string StatusDescription
);

public static class ScheduleCalculator
{
    public const int DefaultDelayToleranceSeconds = 20;

    /// <summary>
    /// Pure function determining if a given wall-clock instant is within the active scheduling window.
    /// Supports daytime windows, all-day windows, and overnight windows spanning midnight.
    /// Overnight windows use start-day anchoring (e.g. Friday 22:00 to 04:00 is active until 03:59 Saturday if Friday is enabled).
    /// </summary>
    public static bool IsInActiveWindow(
        DateTimeOffset instant,
        TimeOnly start,
        TimeOnly end,
        bool isAllDay,
        IReadOnlySet<DayOfWeek> activeDays)
    {
        if (isAllDay)
        {
            return activeDays.Contains(instant.DayOfWeek);
        }

        var time = TimeOnly.FromTimeSpan(instant.TimeOfDay);

        if (start < end)
        {
            // Standard daytime window (inclusive start, exclusive end)
            bool timeMatches = time >= start && time < end;
            return timeMatches && activeDays.Contains(instant.DayOfWeek);
        }
        else if (start > end)
        {
            // Overnight window spanning midnight (e.g. 22:00 to 04:00)
            if (time >= start)
            {
                // Before midnight, anchored to current day
                return activeDays.Contains(instant.DayOfWeek);
            }
            else if (time < end)
            {
                // After midnight, anchored to previous day
                var previousDay = instant.AddDays(-1).DayOfWeek;
                return activeDays.Contains(previousDay);
            }

            return false;
        }
        else
        {
            // Start == End is treated as 24h if enabled, else empty window
            return false;
        }
    }

    /// <summary>
    /// Computes the next scheduled boundary alignment (e.g., :00, :15, :30, :45) that falls within an active window.
    /// Searches forward up to 8 days to find the next occurrence across weekdays and window boundaries.
    /// </summary>
    public static DateTimeOffset? CalculateNextOccurrence(
        DateTimeOffset currentWallClock,
        int intervalMinutes,
        TimeOnly windowStart,
        TimeOnly windowEnd,
        bool isAllDay,
        IReadOnlySet<DayOfWeek> activeDays)
    {
        if (intervalMinutes <= 0 || activeDays.Count == 0)
        {
            return null;
        }

        // Align candidate to the next strict interval boundary
        long totalSeconds = (currentWallClock.Minute * 60) + currentWallClock.Second;
        int intervalSec = intervalMinutes * 60;
        int remainderSec = (int)(totalSeconds % intervalSec);

        DateTimeOffset candidate;
        if (remainderSec == 0 && currentWallClock.Second == 0 && currentWallClock.Millisecond == 0)
        {
            // Exactly on the boundary, advance to next boundary
            candidate = currentWallClock.AddMinutes(intervalMinutes);
        }
        else
        {
            int addSec = intervalSec - remainderSec;
            candidate = currentWallClock.AddSeconds(addSec).AddMilliseconds(-currentWallClock.Millisecond);
        }

        // Search candidate instants up to 8 days forward
        DateTimeOffset searchLimit = currentWallClock.AddDays(8);
        while (candidate <= searchLimit)
        {
            if (IsInActiveWindow(candidate, windowStart, windowEnd, isAllDay, activeDays))
            {
                return candidate;
            }

            candidate = candidate.AddMinutes(intervalMinutes);
        }

        return null;
    }

    /// <summary>
    /// Evaluates whether an announcement should be triggered right now at the given wall clock instant.
    /// Respects the 20-second delay tolerance window and suppresses duplicates for backward clock shifts.
    /// </summary>
    public static ScheduleCalculationResult Evaluate(
        DateTimeOffset currentWallClock,
        bool isEnabled,
        bool isPaused,
        int intervalMinutes,
        TimeOnly windowStart,
        TimeOnly windowEnd,
        bool isAllDay,
        IReadOnlySet<DayOfWeek> activeDays,
        DateTimeOffset? lastAnnouncedInstant = null,
        int delayToleranceSeconds = DefaultDelayToleranceSeconds)
    {
        if (!isEnabled)
        {
            return new ScheduleCalculationResult(null, false, false, "Disabled");
        }

        if (isPaused)
        {
            return new ScheduleCalculationResult(null, false, false, "Paused");
        }

        bool isActiveNow = IsInActiveWindow(currentWallClock, windowStart, windowEnd, isAllDay, activeDays);

        // Find the slot boundary that corresponds to the current wall-clock
        long minuteSec = (currentWallClock.Minute * 60) + currentWallClock.Second;
        int intervalSec = intervalMinutes * 60;
        long secondsPastBoundary = minuteSec % intervalSec;

        DateTimeOffset currentSlotBoundary = currentWallClock
            .AddSeconds(-secondsPastBoundary)
            .AddMilliseconds(-currentWallClock.Millisecond);

        bool shouldTriggerNow = false;
        string status = isActiveNow ? "Active" : "Outside active window";

        if (isActiveNow)
        {
            // Boundary check: is the current clock within [boundary, boundary + tolerance]?
            bool withinTolerance = secondsPastBoundary <= delayToleranceSeconds;

            // Duplicate suppression: has this boundary already been announced in this session?
            bool alreadyAnnounced = lastAnnouncedInstant.HasValue &&
                                    Math.Abs((currentSlotBoundary - lastAnnouncedInstant.Value).TotalSeconds) < (intervalMinutes * 30);

            if (withinTolerance && !alreadyAnnounced)
            {
                shouldTriggerNow = true;
                status = "Triggering announcement";
            }
            else if (!withinTolerance)
            {
                status = "Awaiting next boundary";
            }
            else
            {
                status = "Slot already announced";
            }
        }

        DateTimeOffset? next = CalculateNextOccurrence(
            currentWallClock,
            intervalMinutes,
            windowStart,
            windowEnd,
            isAllDay,
            activeDays);

        return new ScheduleCalculationResult(next, isActiveNow, shouldTriggerNow, status);
    }
}

