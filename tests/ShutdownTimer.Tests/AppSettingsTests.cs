using System;
using System.IO;
using ShutdownTimer.Core;
using Xunit;

namespace ShutdownTimer.Tests;

/// <summary>Каждый тест работает в собственной временной папке и убирает за собой.</summary>
public class AppSettingsTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "ShutdownTimerTests_" + Guid.NewGuid().ToString("N"));
    string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Defaults_MatchDocumentedValues()
    {
        var s = new AppSettings();

        Assert.False(s.ExactTimeMode);
        Assert.Equal(0, s.TimerHours);
        Assert.Equal(30, s.TimerMinutes);
        Assert.Null(s.ClockHour);
        Assert.Null(s.ClockMinute);
        Assert.False(s.Restart);
        Assert.True(s.Reminders);
        Assert.Equal("auto", s.Language);
        Assert.False(s.AutoStart);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var s = AppSettings.Load(FilePath);

        Assert.Equal(30, s.TimerMinutes);
        Assert.True(s.Reminders);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var original = new AppSettings
        {
            ExactTimeMode = true,
            TimerHours = 2,
            TimerMinutes = 15,
            ClockHour = 23,
            ClockMinute = 40,
            Restart = true,
            Reminders = false,
            Language = "en",
            AutoStart = true
        };

        original.Save(FilePath);
        var loaded = AppSettings.Load(FilePath);

        Assert.True(loaded.ExactTimeMode);
        Assert.Equal(2, loaded.TimerHours);
        Assert.Equal(15, loaded.TimerMinutes);
        Assert.Equal(23, loaded.ClockHour);
        Assert.Equal(40, loaded.ClockMinute);
        Assert.True(loaded.Restart);
        Assert.False(loaded.Reminders);
        Assert.Equal("en", loaded.Language);
        Assert.True(loaded.AutoStart);
    }

    [Fact]
    public void Save_CreatesMissingFolder()
    {
        Assert.False(Directory.Exists(_dir));

        new AppSettings().Save(FilePath);

        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void Save_DoesNotLeaveTemporaryFile()
    {
        new AppSettings().Save(FilePath);

        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Save_OverwritesPreviousFile()
    {
        var first = new AppSettings { TimerMinutes = 10 };
        first.Save(FilePath);

        var second = new AppSettings { TimerMinutes = 45 };
        second.Save(FilePath);

        Assert.Equal(45, AppSettings.Load(FilePath).TimerMinutes);
    }

    [Fact]
    public void Save_UnsetClockTimeStaysNull()
    {
        new AppSettings().Save(FilePath);

        var loaded = AppSettings.Load(FilePath);

        Assert.Null(loaded.ClockHour);
        Assert.Null(loaded.ClockMinute);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ это не json");

        var s = AppSettings.Load(FilePath);

        Assert.Equal(30, s.TimerMinutes);
        Assert.True(s.Reminders);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "");

        var s = AppSettings.Load(FilePath);

        Assert.Equal(30, s.TimerMinutes);
    }

    [Fact]
    public void Load_JsonNull_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "null");

        var s = AppSettings.Load(FilePath);

        Assert.Equal(30, s.TimerMinutes);
    }

    [Fact]
    public void Load_WrongValueType_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"TimerHours\":\"много\"}");

        var s = AppSettings.Load(FilePath);

        Assert.Equal(0, s.TimerHours);
        Assert.Equal(30, s.TimerMinutes);
    }

    [Fact]
    public void Load_InvalidLanguage_FallsBackToAuto()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"Language\":\"de\"}");

        var s = AppSettings.Load(FilePath);

        Assert.Equal("auto", s.Language);
    }

    [Fact]
    public void Load_TooLargeValues_AreClamped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath,
            "{\"TimerHours\":99,\"TimerMinutes\":120,\"ClockHour\":30,\"ClockMinute\":75}");

        var s = AppSettings.Load(FilePath);

        Assert.Equal(23, s.TimerHours);
        Assert.Equal(59, s.TimerMinutes);
        Assert.Equal(23, s.ClockHour);
        Assert.Equal(59, s.ClockMinute);
    }

    [Fact]
    public void Load_NegativeValues_AreClampedToZero()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath,
            "{\"TimerHours\":-3,\"TimerMinutes\":-5,\"ClockHour\":-1,\"ClockMinute\":-9}");

        var s = AppSettings.Load(FilePath);

        Assert.Equal(0, s.TimerHours);
        Assert.Equal(0, s.TimerMinutes);
        Assert.Equal(0, s.ClockHour);
        Assert.Equal(0, s.ClockMinute);
    }

    [Fact]
    public void Load_MissingFields_KeepDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"Restart\":true}");

        var s = AppSettings.Load(FilePath);

        Assert.True(s.Restart);
        Assert.Equal(30, s.TimerMinutes);
        Assert.True(s.Reminders);
    }

    [Fact]
    public void FilePath_PointsToSettingsJsonInAppDataFolder()
    {
        Assert.EndsWith(Path.Combine("ShutdownTimer", "settings.json"), AppSettings.FilePath);
    }
}
