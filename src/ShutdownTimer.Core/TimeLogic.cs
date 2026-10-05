using System;
using System.Collections.Generic;

namespace ShutdownTimer.Core;

/// <summary>
/// Вся арифметика времени приложения. Класс не зависит от WPF и системных часов
/// (текущее время передаётся параметром), поэтому его легко проверять тестами.
/// </summary>
public static class TimeLogic
{
    /// <summary>Минимальная задержка, с которой можно запустить таймер.</summary>
    public const int MinimumDelaySeconds = 10;

    /// <summary>За сколько секунд до конца показывать напоминания (от дальнего к ближнему).</summary>
    public static readonly IReadOnlyList<int> ReminderMarks = new[] { 600, 300, 60, 30 };

    /// <summary>
    /// Ближайший момент, когда на часах будет <paramref name="hour"/>:<paramref name="minute"/>.
    /// Если это время сегодня уже наступило (или наступает прямо сейчас), возвращается завтрашний день.
    /// </summary>
    public static DateTime NextClockTime(DateTime now, int hour, int minute)
    {
        var t = now.Date.AddHours(hour).AddMinutes(minute);
        return t <= now ? t.AddDays(1) : t;
    }

    /// <summary>Момент срабатывания таймера «через N часов M минут».</summary>
    public static DateTime TimerTarget(DateTime now, int hours, int minutes) =>
        now.AddHours(hours).AddMinutes(minutes);

    /// <summary>Достаточно ли далеко цель, чтобы запускать отсчёт (не меньше <see cref="MinimumDelaySeconds"/>).</summary>
    public static bool IsDelayValid(DateTime now, DateTime target) =>
        (target - now).TotalSeconds >= MinimumDelaySeconds;

    /// <summary>Сколько целых секунд осталось. Округляется вверх, не бывает отрицательным.</summary>
    public static int RemainingSeconds(DateTime now, DateTime target) =>
        (int)Math.Ceiling(Math.Max(0, (target - now).TotalSeconds));

    /// <summary>Секунды в виде «ЧЧ:ММ:СС».</summary>
    public static string FormatCountdown(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        return $"{totalSeconds / 3600:00}:{totalSeconds / 60 % 60:00}:{totalSeconds % 60:00}";
    }

    /// <summary>Интервал для подсказки: «2 ч 5 мин» или «15 мин». Неполная минута округляется вверх.</summary>
    public static string FormatSpan(TimeSpan d)
    {
        int h = (int)d.TotalHours, m = d.Minutes + (d.Seconds > 0 ? 1 : 0);
        if (m == 60) { h++; m = 0; }
        return h > 0 ? $"{h} ч {m} мин" : $"{m} мин";
    }

    /// <summary>Какая доля отсчёта осталась: 1 в начале, 0 в конце. Всегда в диапазоне 0..1.</summary>
    public static double RemainingFraction(DateTime start, DateTime target, DateTime now)
    {
        double total = Math.Max(1, (target - start).TotalSeconds);
        double left = Math.Max(0, (target - now).TotalSeconds);
        return Math.Min(1, left / total);
    }

    /// <summary>Значение с циклическим переходом: для диапазона 0..59 число 60 даёт 0, а -1 даёт 59.</summary>
    public static int Wrap(int value, int min, int max)
    {
        int n = max - min + 1;
        return ((value - min) % n + n) % n + min;
    }

    /// <summary>
    /// Определяет, какие напоминания нужно показать сейчас.
    /// Все пройденные пороги запоминаются в <paramref name="fired"/> и больше не повторяются.
    /// Порог, не меньший всей длительности таймера, считается пройденным, но не возвращается
    /// (напоминать «за 10 минут» при таймере на 8 минут бессмысленно).
    /// </summary>
    public static IReadOnlyList<int> TakeDueReminders(double secondsLeft, double totalSeconds, ISet<int> fired)
    {
        var due = new List<int>();
        foreach (var mark in ReminderMarks)
        {
            if (fired.Contains(mark) || secondsLeft > mark) continue;
            fired.Add(mark);
            if (mark < totalSeconds - 1) due.Add(mark);
        }
        return due;
    }

    /// <summary>Порог напоминания в виде текста: «10 мин», «30 сек».</summary>
    public static string FormatMark(int seconds) =>
        seconds >= 60 ? $"{seconds / 60} мин" : $"{seconds} сек";
}
