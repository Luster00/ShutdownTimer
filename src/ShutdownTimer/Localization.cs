using System;
using System.Globalization;
using ShutdownTimer.Core;

namespace ShutdownTimer;

public static class Localization
{
    public static bool IsEnglish(string language) => Resolve(language) == "en";

    public static string Resolve(string language)
    {
        if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)) return "en";
        if (string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase)) return "ru";
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? "en" : "ru";
    }

    public static string Text(string language, string key)
    {
        bool en = IsEnglish(language);
        return key switch
        {
            "minimize" => en ? "Minimize" : "Свернуть",
            "hide" => en ? "Hide to tray" : "Скрыть в трей",
            "timer" => en ? "Timer" : "Таймер",
            "exactTime" => en ? "Exact time" : "Точное время",
            "shutdown" => en ? "Shut down" : "Выключить",
            "restart" => en ? "Restart" : "Перезагрузить",
            "reminders" => en ? "Remind me 10, 5 and 1 minute before" : "Напоминать за 10, 5 и 1 минуту",
            "start" => en ? "Start" : "Запустить",
            "cancel" => en ? "Cancel" : "Отменить",
            "cancelShutdown" => en ? "Cancel shutdown" : "Отменить выключение",
            "ready" => en ? "Ready to start" : "Готов к запуску",
            "chooseTime" => en ? "Choose a time before shutdown" : "Выберите время до выключения",
            "triggerAt" => en ? "Will trigger at" : "Сработает в",
            "today" => en ? "Today" : "Сегодня",
            "tomorrow" => en ? "Tomorrow" : "Завтра",
            "through" => en ? "in" : "через",
            "timerStarted" => en ? "Timer started" : "Таймер запущен",
            "invalidTime" => en ? "Set a time greater than zero" : "Укажите время больше нуля",
            "shutdownAt" => en ? "Shutdown at" : "Выключение в",
            "restartAt" => en ? "Restart at" : "Перезагрузка в",
            "shutdownStarting" => en ? "Shutting down…" : "Завершение работы…",
            "shutdownIn" => en ? "Your computer will {0} in {1} seconds. Press “Cancel” to stop it." : "Компьютер {0} через {1} секунд. Нажмите «Отменить», чтобы остановить.",
            "reboot" => en ? "restart" : "перезагрузится",
            "powerOff" => en ? "shut down" : "выключится",
            "remaining" => en ? "Remaining" : "Осталось",
            "ending" => en ? "Ending" : "Завершение",
            "timeLeft" => en ? "Time left" : "До выключения осталось",
            "trayRunning" => en ? "The app is running in the tray. Double-click the icon to open it." : "Приложение работает в трее. Двойной клик по значку — открыть.",
            "exitQuestion" => en ? "The timer is active. Exit and cancel the shutdown?" : "Таймер активен. Выйти и отменить выключение?",
            "settings" => en ? "Settings" : "Настройки",
            "open" => en ? "Open" : "Открыть",
            "exit" => en ? "Exit" : "Выход",
            "hours" => en ? "hours" : "часы",
            "minutes" => en ? "min" : "мин",
            "settingsTitle" => en ? "Settings" : "Настройки",
            "language" => en ? "Language" : "Язык",
            "languageAuto" => en ? "Automatic (Windows)" : "Автоматически (Windows)",
            "russian" => "Русский",
            "english" => "English",
            "startup" => en ? "Start with Windows" : "Запускать вместе с Windows",
            "startupHint" => en ? "Launch Shutdown Timer automatically after you sign in to Windows." : "Автоматически запускать Shutdown Timer после входа в Windows.",
            "close" => en ? "Done" : "Готово",
            "settingsHint" => en ? "Changes are saved automatically." : "Изменения сохраняются автоматически.",
            "languageChanged" => en ? "Language changed" : "Язык изменён",
            _ => key
        };
    }
}
