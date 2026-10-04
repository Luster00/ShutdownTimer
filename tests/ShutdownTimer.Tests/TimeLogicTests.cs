using System;
using System.Collections.Generic;
using ShutdownTimer.Core;
using Xunit;

namespace ShutdownTimer.Tests;

public class TimeLogicTests
{
    // Фиксированное «сейчас», чтобы тесты не зависели от настоящих часов: среда, 15 января 2025, 14:30:00
    static readonly DateTime Now = new(2025, 1, 15, 14, 30, 0);

    // ---------------- NextClockTime ----------------

    [Fact]
    public void NextClockTime_LaterToday_ReturnsToday()
    {
        var result = TimeLogic.NextClockTime(Now, 23, 40);

        Assert.Equal(new DateTime(2025, 1, 15, 23, 40, 0), result);
    }

    [Fact]
    public void NextClockTime_AlreadyPassedToday_ReturnsTomorrow()
    {
        var result = TimeLogic.NextClockTime(Now, 8, 15);

        Assert.Equal(new DateTime(2025, 1, 16, 8, 15, 0), result);
    }

    [Fact]
    public void NextClockTime_ExactlyNow_ReturnsTomorrow()
    {
        var result = TimeLogic.NextClockTime(Now, 14, 30);

        Assert.Equal(new DateTime(2025, 1, 16, 14, 30, 0), result);
    }

    [Fact]
    public void NextClockTime_OneMinuteAhead_ReturnsToday()
    {
        var result = TimeLogic.NextClockTime(Now, 14, 31);

        Assert.Equal(new DateTime(2025, 1, 15, 14, 31, 0), result);
    }

    [Fact]
    public void NextClockTime_MidnightAfterEvening_ReturnsNextDayMidnight()
    {
        var evening = new DateTime(2025, 1, 15, 23, 50, 0);

        var result = TimeLogic.NextClockTime(evening, 0, 0);

        Assert.Equal(new DateTime(2025, 1, 16, 0, 0, 0), result);
    }

    [Fact]
    public void NextClockTime_AtMonthEnd_RollsOverToNextMonth()
    {
        var lateJanuary = new DateTime(2025, 1, 31, 23, 0, 0);

        var result = TimeLogic.NextClockTime(lateJanuary, 10, 0);

        Assert.Equal(new DateTime(2025, 2, 1, 10, 0, 0), result);
    }

    [Fact]
    public void NextClockTime_AtYearEnd_RollsOverToNextYear()
    {
        var newYearsEve = new DateTime(2025, 12, 31, 22, 0, 0);

        var result = TimeLogic.NextClockTime(newYearsEve, 7, 30);

        Assert.Equal(new DateTime(2026, 1, 1, 7, 30, 0), result);
    }

    [Fact]
    public void NextClockTime_IgnoresSecondsOfNow()
    {
        var withSeconds = new DateTime(2025, 1, 15, 14, 30, 45);

        // 14:30 уже наступило (в 14:30:45), поэтому следующее срабатывание завтра
        var result = TimeLogic.NextClockTime(withSeconds, 14, 30);

        Assert.Equal(new DateTime(2025, 1, 16, 14, 30, 0), result);
    }

    // ---------------- TimerTarget ----------------

    [Fact]
    public void TimerTarget_AddsHoursAndMinutes()
    {
        var result = TimeLogic.TimerTarget(Now, 1, 15);

        Assert.Equal(new DateTime(2025, 1, 15, 15, 45, 0), result);
    }

    [Fact]
    public void TimerTarget_CrossesMidnight()
    {
        var result = TimeLogic.TimerTarget(Now, 10, 0);

        Assert.Equal(new DateTime(2025, 1, 16, 0, 30, 0), result);
    }

    [Fact]
    public void TimerTarget_ZeroDelay_ReturnsNow()
    {
        Assert.Equal(Now, TimeLogic.TimerTarget(Now, 0, 0));
    }

    // ---------------- IsDelayValid ----------------

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(3600, true)]
    [InlineData(-30, false)]
    public void IsDelayValid_RequiresAtLeastTenSeconds(int secondsAhead, bool expected)
    {
        var target = Now.AddSeconds(secondsAhead);

        Assert.Equal(expected, TimeLogic.IsDelayValid(Now, target));
    }

    // ---------------- RemainingSeconds ----------------

    [Fact]
    public void RemainingSeconds_ExactSeconds_ReturnsThem()
    {
        Assert.Equal(90, TimeLogic.RemainingSeconds(Now, Now.AddSeconds(90)));
    }

    [Fact]
    public void RemainingSeconds_RoundsUp()
    {
        Assert.Equal(2, TimeLogic.RemainingSeconds(Now, Now.AddMilliseconds(1200)));
        Assert.Equal(1, TimeLogic.RemainingSeconds(Now, Now.AddMilliseconds(1)));
    }

    [Fact]
    public void RemainingSeconds_AfterTarget_IsZero()
    {
        Assert.Equal(0, TimeLogic.RemainingSeconds(Now, Now.AddSeconds(-5)));
    }

    [Fact]
    public void RemainingSeconds_AtTarget_IsZero()
    {
        Assert.Equal(0, TimeLogic.RemainingSeconds(Now, Now));
    }

    // ---------------- FormatCountdown ----------------

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(5, "00:00:05")]
    [InlineData(59, "00:00:59")]
    [InlineData(60, "00:01:00")]
    [InlineData(1800, "00:30:00")]
    [InlineData(3599, "00:59:59")]
    [InlineData(3600, "01:00:00")]
    [InlineData(3725, "01:02:05")]
    [InlineData(86399, "23:59:59")]
    public void FormatCountdown_FormatsAsHoursMinutesSeconds(int seconds, string expected)
    {
        Assert.Equal(expected, TimeLogic.FormatCountdown(seconds));
    }

    [Fact]
    public void FormatCountdown_NegativeValue_ShowsZero()
    {
        Assert.Equal("00:00:00", TimeLogic.FormatCountdown(-10));
    }

    // ---------------- FormatSpan ----------------

    [Theory]
    [InlineData(0, "0 мин")]
    [InlineData(1, "1 мин")]
    [InlineData(30, "1 мин")]
    [InlineData(60, "1 мин")]
    [InlineData(61, "2 мин")]
    [InlineData(900, "15 мин")]
    [InlineData(3599, "1 ч 0 мин")]
    [InlineData(3600, "1 ч 0 мин")]
    [InlineData(3601, "1 ч 1 мин")]
    [InlineData(3661, "1 ч 2 мин")]
    [InlineData(7500, "2 ч 5 мин")]
    public void FormatSpan_RoundsPartialMinuteUp(int seconds, string expected)
    {
        Assert.Equal(expected, TimeLogic.FormatSpan(TimeSpan.FromSeconds(seconds)));
    }

    // ---------------- RemainingFraction ----------------

    [Fact]
    public void RemainingFraction_AtStart_IsOne()
    {
        var target = Now.AddMinutes(10);

        Assert.Equal(1.0, TimeLogic.RemainingFraction(Now, target, Now), 6);
    }

    [Fact]
    public void RemainingFraction_Halfway_IsHalf()
    {
        var target = Now.AddMinutes(10);

        Assert.Equal(0.5, TimeLogic.RemainingFraction(Now, target, Now.AddMinutes(5)), 6);
    }

    [Fact]
    public void RemainingFraction_AtTarget_IsZero()
    {
        var target = Now.AddMinutes(10);

        Assert.Equal(0.0, TimeLogic.RemainingFraction(Now, target, target), 6);
    }

    [Fact]
    public void RemainingFraction_AfterTarget_StaysZero()
    {
        var target = Now.AddMinutes(10);

        Assert.Equal(0.0, TimeLogic.RemainingFraction(Now, target, target.AddMinutes(3)), 6);
    }

    [Fact]
    public void RemainingFraction_BeforeStart_IsClampedToOne()
    {
        var target = Now.AddMinutes(10);

        Assert.Equal(1.0, TimeLogic.RemainingFraction(Now, target, Now.AddMinutes(-2)), 6);
    }

    [Fact]
    public void RemainingFraction_ZeroLengthInterval_DoesNotDivideByZero()
    {
        Assert.Equal(0.0, TimeLogic.RemainingFraction(Now, Now, Now), 6);
    }

    // ---------------- Wrap ----------------

    [Theory]
    [InlineData(5, 0, 59, 5)]
    [InlineData(0, 0, 59, 0)]
    [InlineData(59, 0, 59, 59)]
    [InlineData(60, 0, 59, 0)]
    [InlineData(61, 0, 59, 1)]
    [InlineData(-1, 0, 59, 59)]
    [InlineData(-60, 0, 59, 0)]
    [InlineData(-61, 0, 59, 59)]
    [InlineData(24, 0, 23, 0)]
    [InlineData(-1, 0, 23, 23)]
    [InlineData(7, 5, 9, 7)]
    [InlineData(10, 5, 9, 5)]
    [InlineData(4, 5, 9, 9)]
    public void Wrap_WrapsAroundRange(int value, int min, int max, int expected)
    {
        Assert.Equal(expected, TimeLogic.Wrap(value, min, max));
    }

    // ---------------- TakeDueReminders ----------------

    [Fact]
    public void TakeDueReminders_FarFromAnyMark_ReturnsNothing()
    {
        var fired = new HashSet<int>();

        var due = TimeLogic.TakeDueReminders(900, 3600, fired);

        Assert.Empty(due);
        Assert.Empty(fired);
    }

    [Fact]
    public void TakeDueReminders_AtTenMinutes_ReturnsTenMinuteMark()
    {
        var fired = new HashSet<int>();

        var due = TimeLogic.TakeDueReminders(600, 3600, fired);

        Assert.Equal(new[] { 600 }, due);
        Assert.Contains(600, fired);
    }

    [Fact]
    public void TakeDueReminders_JustAboveMark_ReturnsNothing()
    {
        var fired = new HashSet<int>();

        var due = TimeLogic.TakeDueReminders(600.5, 3600, fired);

        Assert.Empty(due);
    }

    [Fact]
    public void TakeDueReminders_SameMarkIsNotRepeated()
    {
        var fired = new HashSet<int>();

        TimeLogic.TakeDueReminders(600, 3600, fired);
        var second = TimeLogic.TakeDueReminders(599, 3600, fired);

        Assert.Empty(second);
    }

    [Fact]
    public void TakeDueReminders_WalkThroughWholeCountdown_FiresEachMarkOnce()
    {
        var fired = new HashSet<int>();
        var all = new List<int>();

        for (double left = 3600; left >= 0; left -= 1)
            all.AddRange(TimeLogic.TakeDueReminders(left, 3600, fired));

        Assert.Equal(new[] { 600, 300, 60, 30 }, all);
    }

    [Fact]
    public void TakeDueReminders_ShortTimer_SkipsMarksLongerThanTimer()
    {
        // Таймер на 8 минут: напоминание «за 10 минут» невозможно, но порог считается пройденным
        var fired = new HashSet<int>();

        var due = TimeLogic.TakeDueReminders(480, 480, fired);

        Assert.Empty(due);
        Assert.Contains(600, fired);
        Assert.DoesNotContain(300, fired);
    }

    [Fact]
    public void TakeDueReminders_MarkEqualToTotal_IsNotReported()
    {
        var fired = new HashSet<int>();

        var due = TimeLogic.TakeDueReminders(600, 600, fired);

        Assert.Empty(due);
        Assert.Contains(600, fired);
    }

    [Fact]
    public void TakeDueReminders_OneMinuteTimer_OnlyThirtySecondsReminder()
    {
        var fired = new HashSet<int>();

        var atStart = TimeLogic.TakeDueReminders(60, 60, fired);
        var later = TimeLogic.TakeDueReminders(30, 60, fired);

        Assert.Empty(atStart);
        Assert.Equal(new[] { 30 }, later);
    }

    [Fact]
    public void TakeDueReminders_AfterLongPause_ReturnsAllMissedMarksInOrder()
    {
        // Например, компьютер вышел из сна, когда до конца осталось 20 секунд
        var fired = new HashSet<int>();

        var due = TimeLogic.TakeDueReminders(20, 3600, fired);

        Assert.Equal(new[] { 600, 300, 60, 30 }, due);
    }

    [Fact]
    public void TakeDueReminders_MarksListIsDescending()
    {
        for (int i = 1; i < TimeLogic.ReminderMarks.Count; i++)
            Assert.True(TimeLogic.ReminderMarks[i - 1] > TimeLogic.ReminderMarks[i]);
    }

    // ---------------- FormatMark ----------------

    [Theory]
    [InlineData(600, "10 мин")]
    [InlineData(300, "5 мин")]
    [InlineData(60, "1 мин")]
    [InlineData(30, "30 сек")]
    [InlineData(10, "10 сек")]
    public void FormatMark_UsesMinutesFromOneMinute(int seconds, string expected)
    {
        Assert.Equal(expected, TimeLogic.FormatMark(seconds));
    }
}
