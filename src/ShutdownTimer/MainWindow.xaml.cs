using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ShutdownTimer.Core;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace ShutdownTimer;

public partial class MainWindow : Window
{
    const int FinalSeconds = 20;          // сколько секунд даём на отмену после команды shutdown
    const double C = 110, R = 104;        // центр и радиус кольца

    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly WF.NotifyIcon _tray;
    readonly WF.ToolStripMenuItem _openItem;
    readonly WF.ToolStripMenuItem _cancelItem;
    readonly WF.ToolStripMenuItem _exitItem;
    readonly Stepper _th, _tm, _ch, _cm;
    readonly HashSet<int> _fired = new();
    readonly AppSettings _settings = AppSettings.Load();
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    DateTime _start, _target;
    bool _active, _final, _exit, _hintShown;
    bool _clockTouched;   // время для точного режима сохраняем, только если пользователь его выбирал
    int _lastSec = -1;
    int _ringGen;         // «поколение» анимации кольца: устаревшие обработчики Completed игнорируются

    bool IsTimerMode => RbTimer.IsChecked == true;
    bool IsRestart => RbRestart.IsChecked == true;

    // Окно действительно видно на экране (не скрыто в трей и не свёрнуто)
    bool Shown => IsVisible && WindowState != WindowState.Minimized;

    // Доля кольца: 1 - полное, 0 - пустое. Значение анимируется средствами WPF,
    // поэтому дуга движется плавно (до 60 кадров в секунду), а не шагами по тику таймера.
    public static readonly DependencyProperty RingFractionProperty = DependencyProperty.Register(
        nameof(RingFraction), typeof(double), typeof(MainWindow),
        new PropertyMetadata(0.0, (d, e) => ((MainWindow)d).DrawArc((double)e.NewValue)));

    public double RingFraction
    {
        get => (double)GetValue(RingFractionProperty);
        set => SetValue(RingFractionProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        _settings.AutoStart = StartupManager.IsEnabled();
        var later = DateTime.Now.AddHours(1);
        _th = new Stepper(Localization.Text(_settings.Language, "hours"), 0, 23, _settings.TimerHours, this);
        _tm = new Stepper(Localization.Text(_settings.Language, "minutes"), 0, 59, _settings.TimerMinutes, this);
        _ch = new Stepper(Localization.Text(_settings.Language, "hours"), 0, 23, _settings.ClockHour ?? later.Hour, this);
        _cm = new Stepper(Localization.Text(_settings.Language, "minutes"), 0, 59, _settings.ClockMinute ?? 0, this);

        TimerHost.Children.Add(_th); TimerHost.Children.Add(Colon()); TimerHost.Children.Add(_tm);
        ClockHost.Children.Add(_ch); ClockHost.Children.Add(Colon()); ClockHost.Children.Add(_cm);
        foreach (var s in new[] { _th, _tm, _ch, _cm }) s.Changed += OnInputChanged;

        _clockTouched = _settings.ClockHour != null;
        _ch.Changed += () => _clockTouched = true;
        _cm.Changed += () => _clockTouched = true;

        // --- восстановление сохранённых настроек ---
        RbClock.IsChecked = _settings.ExactTimeMode;
        RbTimer.IsChecked = !_settings.ExactTimeMode;
        PanelTimer.Visibility = _settings.ExactTimeMode ? Visibility.Collapsed : Visibility.Visible;
        PanelClock.Visibility = _settings.ExactTimeMode ? Visibility.Visible : Visibility.Collapsed;
        RbRestart.IsChecked = _settings.Restart;
        RbShutdown.IsChecked = !_settings.Restart;
        Reminders.IsChecked = _settings.Reminders;

        // сохранение с задержкой, чтобы не писать файл на каждый щелчок по ▲ ▼
        _saveTimer.Tick += (_, _) => SaveSettings();
        Application.Current.SessionEnding += (_, _) => SaveSettings();

        // --- трей ---
        var menu = new WF.ContextMenuStrip();
        _openItem = (WF.ToolStripMenuItem)menu.Items.Add(Localization.Text(_settings.Language, "open"), null, (_, _) => ShowFromTray());
        _cancelItem = (WF.ToolStripMenuItem)menu.Items.Add(Localization.Text(_settings.Language, "cancelShutdown"), null, (_, _) => Cancel());
        _cancelItem.Enabled = false;
        menu.Items.Add(new WF.ToolStripSeparator());
        _exitItem = (WF.ToolStripMenuItem)menu.Items.Add(Localization.Text(_settings.Language, "exit"), null, (_, _) => ExitApp());

        _tray = new WF.NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "Shutdown Timer",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => ShowFromTray();

        _tick.Tick += (_, _) => OnTick();
        _tick.Start();

        // Пока окно скрыто в трей или свёрнуто, анимации не нужны: останавливаем их и не тратим процессор
        IsVisibleChanged += (_, _) => OnVisibilityChanged();
        StateChanged += (_, _) => OnVisibilityChanged();

        Loaded += (_, _) =>
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));
            var pop = new DoubleAnimation(0.93, 1, TimeSpan.FromMilliseconds(450)) { EasingFunction = ease };
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            Breathe(true);
            ApplyLanguage();
            RefreshIdle();
        };
    }

    void ApplyLanguage()
    {
        bool en = Localization.IsEnglish(_settings.Language);
        Title = "Shutdown Timer";
        MinimizeButton.ToolTip = Localization.Text(_settings.Language, "minimize");
        CloseButton.ToolTip = Localization.Text(_settings.Language, "hide");
        SettingsButton.ToolTip = Localization.Text(_settings.Language, "settings");
        RbTimer.Content = Localization.Text(_settings.Language, "timer");
        RbClock.Content = Localization.Text(_settings.Language, "exactTime");
        RbShutdown.Content = Localization.Text(_settings.Language, "shutdown");
        RbRestart.Content = Localization.Text(_settings.Language, "restart");
        Reminders.Content = Localization.Text(_settings.Language, "reminders");
        Preset15.Content = en ? "15 min" : "15 мин";
        Preset30.Content = en ? "30 min" : "30 мин";
        Preset60.Content = en ? "1 hour" : "1 час";
        Preset120.Content = en ? "2 hours" : "2 часа";
        _th.SetLabel(Localization.Text(_settings.Language, "hours"));
        _ch.SetLabel(Localization.Text(_settings.Language, "hours"));
        _tm.SetLabel(Localization.Text(_settings.Language, "minutes"));
        _cm.SetLabel(Localization.Text(_settings.Language, "minutes"));
        _openItem.Text = Localization.Text(_settings.Language, "open");
        _cancelItem.Text = Localization.Text(_settings.Language, "cancelShutdown");
        _exitItem.Text = Localization.Text(_settings.Language, "exit");
        _tray.Text = "Shutdown Timer";
        SetUi(_active);
    }

    void Settings_Click(object s, RoutedEventArgs e)
    {
        if (_active) return;
        var window = new SettingsWindow(_settings, () =>
        {
            ApplyLanguage();
            RefreshIdle();
            SaveSettings();
        }) { Owner = this };
        window.ShowDialog();
    }

    TextBlock Colon() => new()
    {
        Text = ":", FontSize = 40, FontWeight = FontWeights.Light,
        Foreground = (Brush)FindResource("TextMutedBrush"), Opacity = 0.6,
        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -6, 0, 0)
    };

    // ---------------- Окно / трей ----------------

    void TitleBar_Drag(object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object s, RoutedEventArgs e) => HideToTray();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exit) { e.Cancel = true; HideToTray(); }
        base.OnClosing(e);
    }

    void HideToTray()
    {
        Hide();
        if (_hintShown) return;
        _hintShown = true;
        _tray.ShowBalloonTip(3000, "Shutdown Timer",
            Localization.Text(_settings.Language, "trayRunning"), WF.ToolTipIcon.Info);
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Topmost = true;      // хитрость, чтобы окно гарантированно вышло на передний план
        Topmost = false;
        Activate();
    }

    void ExitApp()
    {
        if (_active && !_final &&
            MessageBox.Show(Localization.Text(_settings.Language, "exitQuestion"), "Shutdown Timer",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (_active) Cancel();
        SaveSettings();
        _exit = true;
        _tray.Visible = false;
        _tray.Dispose();
        Application.Current.Shutdown();
    }

    SD.Icon MakeIcon()
    {
        // цвета значка берутся из той же палитры, что и интерфейс
        var accent = (Color)FindResource("AccentColor");
        var glyph = (Color)FindResource("OnAccentColor");

        using var bmp = new SD.Bitmap(32, 32);
        using (var g = SD.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var fill = new SD.SolidBrush(SD.Color.FromArgb(accent.R, accent.G, accent.B));
            g.FillEllipse(fill, 1, 1, 30, 30);
            using var pen = new SD.Pen(SD.Color.FromArgb(glyph.R, glyph.G, glyph.B), 3f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            g.DrawArc(pen, 8, 8, 16, 16, -60, 300);
            g.DrawLine(pen, 16, 6, 16, 15);
        }
        return SD.Icon.FromHandle(bmp.GetHicon());
    }

    // ---------------- Ввод ----------------

    void Mode_Click(object s, RoutedEventArgs e)
    {
        var show = IsTimerMode ? PanelTimer : PanelClock;
        var hide = IsTimerMode ? PanelClock : PanelTimer;
        if (show.Visibility == Visibility.Visible) return;

        hide.Visibility = Visibility.Collapsed;
        show.Visibility = Visibility.Visible;
        show.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        ((TranslateTransform)show.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(IsTimerMode ? -36 : 36, 0, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        RefreshIdle();
        ScheduleSave();
    }

    void Chip_Click(object s, RoutedEventArgs e)
    {
        int min = int.Parse((string)((Button)s).Tag);
        _th.Set(min / 60);
        _tm.Set(min % 60);
        RefreshIdle();
        ScheduleSave();
    }

    void Option_Click(object s, RoutedEventArgs e) => ScheduleSave();

    void OnInputChanged()
    {
        RefreshIdle();
        ScheduleSave();
    }

    // ---------------- Настройки ----------------

    void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    void SaveSettings()
    {
        _saveTimer.Stop();
        _settings.ExactTimeMode = !IsTimerMode;
        _settings.TimerHours = _th.Value;
        _settings.TimerMinutes = _tm.Value;
        if (_clockTouched)
        {
            _settings.ClockHour = _ch.Value;
            _settings.ClockMinute = _cm.Value;
        }
        _settings.Restart = IsRestart;
        _settings.Reminders = Reminders.IsChecked == true;
        _settings.Save();
    }

    DateTime NextClock() => TimeLogic.NextClockTime(DateTime.Now, _ch.Value, _cm.Value);

    string FormatSpan(TimeSpan span)
    {
        int h = (int)span.TotalHours, m = span.Minutes + (span.Seconds > 0 ? 1 : 0);
        if (m == 60) { h++; m = 0; }
        if (Localization.IsEnglish(_settings.Language)) return h > 0 ? $"{h}h {m}min" : $"{m}min";
        return h > 0 ? $"{h} ч {m} мин" : $"{m} мин";
    }

    string FormatMark(int seconds) => Localization.IsEnglish(_settings.Language)
        ? seconds >= 60 ? $"{seconds / 60} min" : $"{seconds} sec"
        : TimeLogic.FormatMark(seconds);

    void SetHint(string text, bool error = false)
    {
        HintText.Text = text;
        HintText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "TextMutedBrush");
    }

    void RefreshIdle()
    {
        if (_active) return;
        StatusText.Text = Localization.Text(_settings.Language, "ready");
        if (IsTimerMode)
        {
            TimeText.Text = $"{_th.Value:00}:{_tm.Value:00}:00";
            var end = TimeLogic.TimerTarget(DateTime.Now, _th.Value, _tm.Value);
            SetHint(_th.Value == 0 && _tm.Value == 0 ? Localization.Text(_settings.Language, "chooseTime") : $"{Localization.Text(_settings.Language, "triggerAt")} {end:HH:mm}");
        }
        else
        {
            var end = NextClock();
            TimeText.Text = $"{end:HH:mm}";
            string day = Localization.Text(_settings.Language, end.Date == DateTime.Today ? "today" : "tomorrow");
            SetHint($"{day} {end:HH:mm}, {Localization.Text(_settings.Language, "through")} {FormatSpan(end - DateTime.Now)}");
        }
    }

    // ---------------- Запуск / отмена ----------------

    void Go_Click(object s, RoutedEventArgs e)
    {
        if (_active) Cancel(); else Start();
    }

    void Start()
    {
        var now = DateTime.Now;
        var target = IsTimerMode ? TimeLogic.TimerTarget(now, _th.Value, _tm.Value) : NextClock();
        if (!TimeLogic.IsDelayValid(now, target))
        {
            SetHint(Localization.Text(_settings.Language, "invalidTime"), true);
            return;
        }

        SaveSettings();
        _start = now; _target = target;
        _fired.Clear(); _final = false; _active = true; _lastSec = -1;

        string action = Localization.Text(_settings.Language, IsRestart ? "restartAt" : "shutdownAt");
        string tomorrow = target.Date != now.Date ? $" ({Localization.Text(_settings.Language, "tomorrow")})" : "";
        StatusText.Text = $"{action} {target:HH:mm}{tomorrow}";
        SetHint(Localization.Text(_settings.Language, "timerStarted"));
        SetUi(true);
        StartRing(intro: true);
        Pulse();
    }

    void Cancel()
    {
        if (_final) Run("/a");
        _active = _final = false;
        StopRing();
        _tray.Text = "Shutdown Timer";
        _lastSec = -1;
        SetUi(false);
        RefreshIdle();
    }

    void Fire()
    {
        _final = true;
        _start = DateTime.Now;
        _target = _start.AddSeconds(FinalSeconds);
        Run(IsRestart ? $"/r /t {FinalSeconds}" : $"/s /t {FinalSeconds}");
        StartRing(intro: false);

        StatusText.Text = Localization.Text(_settings.Language, "shutdownStarting");
        BtnGo.Content = Localization.Text(_settings.Language, "cancelShutdown");
        string action = Localization.Text(_settings.Language, IsRestart ? "reboot" : "powerOff");
        _tray.ShowBalloonTip(5000, "Shutdown Timer",
            string.Format(Localization.Text(_settings.Language, "shutdownIn"), action, FinalSeconds),
            WF.ToolTipIcon.Warning);
        ShowFromTray();
        Pulse();
    }

    static void Run(string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo("shutdown", args) { CreateNoWindow = true, UseShellExecute = false });
        }
        catch { /* ignore */ }
    }

    void SetUi(bool active)
    {
        BtnGo.Content = active ? Localization.Text(_settings.Language, "cancel") : Localization.Text(_settings.Language, "start");
        BtnGo.Background = (Brush)FindResource(active ? "StopBrush" : "GoBrush");
        BtnGo.Foreground = (Brush)FindResource(active ? "OnStopBrush" : "OnAccentBrush");
        SettingsButton.IsEnabled = !active;
        _cancelItem.Enabled = active;

        foreach (var el in new UIElement[] { ModeBar, InputsGrid, OptionsPanel })
        {
            el.IsEnabled = !active;
            el.BeginAnimation(OpacityProperty, new DoubleAnimation(active ? 0.3 : 1, TimeSpan.FromMilliseconds(250)));
        }
        Breathe(!active);
    }

    // ---------------- Тик ----------------

    void OnTick()
    {
        if (!_active)
        {
            int sec = DateTime.Now.Second;
            if (sec != _lastSec) { _lastSec = sec; RefreshIdle(); }
            return;
        }

        var now = DateTime.Now;
        double total = Math.Max(1, (_target - _start).TotalSeconds);
        double left = Math.Max(0, (_target - now).TotalSeconds);
        int secs = TimeLogic.RemainingSeconds(now, _target);

        TimeText.Text = TimeLogic.FormatCountdown(secs);

        if (secs != _lastSec)
        {
            _lastSec = secs;
            _tray.Text = $"{Localization.Text(_settings.Language, _final ? "ending" : "remaining")} {TimeText.Text}";
        }

        if (_final) return;

        foreach (var mark in TimeLogic.TakeDueReminders(left, total, _fired))
            if (Reminders.IsChecked == true) Remind(mark);

        if (left <= 0) Fire();
    }

    void Remind(int seconds)
    {
        string t = FormatMark(seconds);
        _tray.ShowBalloonTip(6000, "Shutdown Timer",
            $"{Localization.Text(_settings.Language, "timeLeft")} {t}", WF.ToolTipIcon.Info);
        Pulse();
    }

    // ---------------- Анимации ----------------

    // Рисует дугу кольца. Вызывается из свойства RingFraction, пока идёт анимация.
    void DrawArc(double f)
    {
        f = Math.Clamp(f, 0, 0.9999);
        if (f <= 0.0005)
        {
            Arc.Data = Geometry.Empty;
            ArcGlow.Data = Geometry.Empty;
            return;
        }

        double a = -Math.PI / 2 + 2 * Math.PI * f;
        var end = new Point(C + R * Math.Cos(a), C + R * Math.Sin(a));
        var fig = new PathFigure { StartPoint = new Point(C, C - R) };
        fig.Segments.Add(new ArcSegment(end, new Size(R, R), 0, f > 0.5, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry(new[] { fig });
        geometry.Freeze();               // замороженная геометрия рисуется дешевле
        Arc.Data = geometry;
        ArcGlow.Data = geometry;         // то же самое для полупрозрачного «свечения» под дугой
    }

    // Запускает анимацию кольца. intro: сначала дуга «вырастает» за 0,7 с, затем плавно убывает до нуля.
    void StartRing(bool intro)
    {
        int gen = ++_ringGen;
        if (!Shown) return;     // скрытому окну анимация не нужна, при показе она запустится сама

        double total = Math.Max(1, (_target - _start).TotalSeconds);

        void Countdown()
        {
            if (gen != _ringGen || !_active) return;
            double left = Math.Max(0.05, (_target - DateTime.Now).TotalSeconds);
            double from = Math.Min(1, left / total);
            var anim = new DoubleAnimation(from, 0, TimeSpan.FromSeconds(left)) { FillBehavior = FillBehavior.HoldEnd };

            // Чем медленнее движется дуга, тем реже её нужно перерисовывать: около 4 кадров на пиксель, от 4 до 60 к/с
            double fps = Math.Clamp(Math.Ceiling(2 * Math.PI * R / total * 4), 4, 60);
            Timeline.SetDesiredFrameRate(anim, (int)fps);
            BeginAnimation(RingFractionProperty, anim);
        }

        if (!intro) { Countdown(); return; }

        double full = Math.Min(1, Math.Max(0.05, (_target - DateTime.Now).TotalSeconds) / total);
        var grow = new DoubleAnimation(0, full, TimeSpan.FromMilliseconds(700))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        grow.Completed += (_, _) => Countdown();
        BeginAnimation(RingFractionProperty, grow);
    }

    void StopRing()
    {
        _ringGen++;
        BeginAnimation(RingFractionProperty, null);
        RingFraction = 0;
        DrawArc(0);
    }

    void OnVisibilityChanged()
    {
        if (!Shown)
        {
            _ringGen++;
            BeginAnimation(RingFractionProperty, null);
            Breathe(false);
        }
        else if (_active) StartRing(intro: false);
        else Breathe(true);
    }

    void Pulse()
    {
        if (!Shown) return;
        var a = new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(260))
        { AutoReverse = true, EasingFunction = new SineEase() };
        RingScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        RingScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    void Breathe(bool on)
    {
        if (on && Shown)
        {
            var anim = new DoubleAnimation(1, 0.55, TimeSpan.FromMilliseconds(1600))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            Timeline.SetDesiredFrameRate(anim, 30);   // для медленного «дыхания» 30 к/с достаточно
            TimeText.BeginAnimation(OpacityProperty, anim);
        }
        else
        {
            TimeText.BeginAnimation(OpacityProperty, null);
            TimeText.Opacity = 1;
        }
    }
}

/// <summary>Числовой выбор: кнопки ▲ ▼ (с удержанием) и колесо мыши.</summary>
sealed class Stepper : StackPanel
{
    readonly TextBlock _t;
    readonly TextBlock _label;
    readonly int _min, _max;
    int _v;

    public event Action? Changed;

    public int Value
    {
        get => _v;
        set { Set(value); Changed?.Invoke(); }
    }

    /// <summary>Установить значение без события Changed.</summary>
    public void SetLabel(string label) => _label.Text = label;

    public void Set(int value)
    {
        _v = TimeLogic.Wrap(value, _min, _max);
        _t.Text = _v.ToString("00");
        var a = new DoubleAnimation(1.18, 1, TimeSpan.FromMilliseconds(220))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var st = (ScaleTransform)_t.RenderTransform;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    public Stepper(string label, int min, int max, int init, FrameworkElement owner)
    {
        _min = min; _max = max; _v = init;
        var soft = (Style)owner.FindResource("Soft");
        Margin = new Thickness(10, 0, 10, 0);
        Background = Brushes.Transparent;

        _t = new TextBlock
        {
            Text = init.ToString("00"), FontSize = 46, FontWeight = FontWeights.Light,
            Foreground = (Brush)owner.FindResource("TextBrush"), HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1),
            Margin = new Thickness(0, 2, 0, 0)
        };
        _label = new TextBlock
        {
            Text = label, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)owner.FindResource("TextMutedBrush"), Margin = new Thickness(0, 0, 0, 6)
        };

        Children.Add(Btn("▲", 1, soft, owner));
        Children.Add(_t);
        Children.Add(_label);
        Children.Add(Btn("▼", -1, soft, owner));

        MouseWheel += (_, e) => { Value += e.Delta > 0 ? 1 : -1; e.Handled = true; };
    }

    RepeatButton Btn(string glyph, int delta, Style soft, FrameworkElement owner)
    {
        var b = new RepeatButton
        {
            Content = glyph, Style = soft, Width = 64, Height = 26, FontSize = 10, Padding = new Thickness(0),
            Background = (Brush)owner.FindResource("ChipBrush"),
            Foreground = (Brush)owner.FindResource("TextSoftBrush"),
            Delay = 350, Interval = 70, Focusable = false
        };
        b.Click += (_, _) => Value += delta;
        return b;
    }
}
