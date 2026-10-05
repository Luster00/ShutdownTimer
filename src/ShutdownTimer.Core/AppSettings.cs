using System;
using System.IO;
using System.Text.Json;

namespace ShutdownTimer.Core;

/// <summary>Настройки пользователя, которые сохраняются между запусками.</summary>
public sealed class AppSettings
{
    /// <summary>true — режим «Точное время», false — режим «Таймер».</summary>
    public bool ExactTimeMode { get; set; }

    public int TimerHours { get; set; }
    public int TimerMinutes { get; set; } = 30;

    /// <summary>Время для режима «Точное время». null — ещё не выбиралось.</summary>
    public int? ClockHour { get; set; }
    public int? ClockMinute { get; set; }

    /// <summary>true — перезагрузка, false — выключение.</summary>
    public bool Restart { get; set; }

    public bool Reminders { get; set; } = true;

    /// <summary>Язык интерфейса: auto, ru или en. По умолчанию язык Windows.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>true — запускать приложение вместе с Windows.</summary>
    public bool AutoStart { get; set; }

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>%AppData%\ShutdownTimer\settings.json</summary>
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShutdownTimer", "settings.json");

    public static AppSettings Load() => Load(FilePath);

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
                s.Normalize();
                return s;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // повреждённый или недоступный файл не должен мешать запуску
        }
        return new AppSettings();
    }

    public void Save() => Save(FilePath);

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // настройки не критичны: приложение продолжает работать без сохранения
        }
    }

    void Normalize()
    {
        TimerHours = Math.Clamp(TimerHours, 0, 23);
        TimerMinutes = Math.Clamp(TimerMinutes, 0, 59);
        ClockHour = ClockHour is int h ? Math.Clamp(h, 0, 23) : null;
        ClockMinute = ClockMinute is int m ? Math.Clamp(m, 0, 59) : null;
        Language = Language?.ToLowerInvariant() switch
        {
            "ru" => "ru",
            "en" => "en",
            _ => "auto"
        };
    }
}
