using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace ShutdownTimer;

static class StartupManager
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppName = "ShutdownTimer";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch { return false; }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return false;
            if (!enabled)
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                return true;
            }

            var path = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path)) return false;
            key.SetValue(AppName, $"\"{path}\"");
            return true;
        }
        catch { return false; }
    }
}
