using System;
using System.Windows;
using System.Windows.Input;
using ShutdownTimer.Core;

namespace ShutdownTimer;

public partial class SettingsWindow : Window
{
    readonly AppSettings _settings;
    readonly Action _languageChanged;

    public SettingsWindow(AppSettings settings, Action languageChanged)
    {
        InitializeComponent();
        _settings = settings;
        _languageChanged = languageChanged;
        AutoStartToggle.IsChecked = StartupManager.IsEnabled();
        ApplyLanguage();
    }

    void ApplyLanguage()
    {
        Title = Localization.Text(_settings.Language, "settingsTitle");
        TitleText.Text = Localization.Text(_settings.Language, "settingsTitle");
        HintText.Text = Localization.Text(_settings.Language, "settingsHint");
        LanguageLabel.Text = Localization.Text(_settings.Language, "language");
        AutoLanguage.Content = Localization.Text(_settings.Language, "languageAuto");
        RussianLanguage.Content = Localization.Text(_settings.Language, "russian");
        EnglishLanguage.Content = Localization.Text(_settings.Language, "english");
        AutoStartToggle.Content = Localization.Text(_settings.Language, "startup");
        AutoStartHint.Text = Localization.Text(_settings.Language, "startupHint");
        CloseButton.Content = Localization.Text(_settings.Language, "close");

        AutoLanguage.IsChecked = _settings.Language == "auto";
        RussianLanguage.IsChecked = _settings.Language == "ru";
        EnglishLanguage.IsChecked = _settings.Language == "en";
    }

    void Language_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string language) return;
        _settings.Language = language;
        _settings.Save();
        ApplyLanguage();
        _languageChanged();
    }

    void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = AutoStartToggle.IsChecked == true;
        if (!StartupManager.SetEnabled(enabled))
        {
            AutoStartToggle.IsChecked = !enabled;
            return;
        }
        _settings.AutoStart = enabled;
        _settings.Save();
    }

    void TitleBar_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();
}
