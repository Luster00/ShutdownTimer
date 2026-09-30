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
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace ShutdownTimer;

public partial class MainWindow : Window
{
    // За сколько секунд до конца показывать напоминания
    static readonly int[] Marks = { 600, 300, 60, 30 };
    const int FinalSeconds = 20;          // сколько секунд даём на отмену после команды shutdown
    const double C = 110, R = 104;        // центр и радиус кольца

    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly WF.NotifyIcon _tray;
    readonly WF.ToolStripMenuItem _cancelItem;
    readonly Stepper _th, _tm, _ch, _cm;
    readonly HashSet<int> _fired = new();

    DateTime _start, _target, _intro;
    bool _active, _final, _exit, _hintShown;
    int _lastSec = -1;

    bool IsTimerMode => RbTimer.IsChecked == true;
    bool IsRestart => RbRestart.IsChecked == true;

    public MainWindow()
    {
        InitializeComponent();
        var soft = (Style)FindResource("Soft");

        var later = DateTime.Now.AddHours(1);
        _th = new Stepper("часы", 0, 23, 0, soft);
        _tm = new Stepper("мин", 0, 59, 30, soft);
        _ch = new Stepper("часы", 0, 23, later.Hour, soft);
        _cm = new Stepper("мин", 0, 59, 0, soft);

        TimerHost.Children.Add(_th); TimerHost.Children.Add(Colon()); TimerHost.Children.Add(_tm);
        ClockHost.Children.Add(_ch); ClockHost.Children.Add(Colon()); ClockHost.Children.Add(_cm);
        foreach (var s in new[] { _th, _tm, _ch, _cm }) s.Changed += RefreshIdle;

        // --- трей ---
        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowFromTray());
        _cancelItem = (WF.ToolStripMenuItem)menu.Items.Add("Отменить выключение", null, (_, _) => Cancel());
        _cancelItem.Enabled = false;
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitApp());

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

        Loaded += (_, _) =>
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));
            var pop = new DoubleAnimation(0.93, 1, TimeSpan.FromMilliseconds(450)) { EasingFunction = ease };
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            Breathe(true);
            RefreshIdle();
        };
    }

    static TextBlock Colon() => new()
    {
        Text = ":", FontSize = 40, FontWeight = FontWeights.Light,
        Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x63, 0x90)),
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
            "Приложение работает в трее. Двойной клик по значку — открыть.", WF.ToolTipIcon.Info);
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
            MessageBox.Show("Таймер активен. Выйти и отменить выключение?", "Shutdown Timer",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (_active) Cancel();
        _exit = true;
        _tray.Visible = false;
        _tray.Dispose();
        Application.Current.Shutdown();
    }

    static SD.Icon MakeIcon()
    {
        using var bmp = new SD.Bitmap(32, 32);
        using (var g = SD.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var fill = new SD.SolidBrush(SD.Color.FromArgb(124, 92, 255));
            g.FillEllipse(fill, 1, 1, 30, 30);
            using var pen = new SD.Pen(SD.Color.White, 3f)
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
    }

    void Chip_Click(object s, RoutedEventArgs e)
    {
        int min = int.Parse((string)((Button)s).Tag);
        _th.Set(min / 60);
        _tm.Set(min % 60);
        RefreshIdle();
    }

    DateTime NextClock()
    {
        var now = DateTime.Now;
        var t = now.Date.AddHours(_ch.Value).AddMinutes(_cm.Value);
        return t <= now ? t.AddDays(1) : t;
    }

    static string Span(TimeSpan d)
    {
        int h = (int)d.TotalHours, m = d.Minutes + (d.Seconds > 0 ? 1 : 0);
        if (m == 60) { h++; m = 0; }
        return h > 0 ? $"{h} ч {m} мин" : $"{m} мин";
    }

    void SetHint(string text, bool error = false)
    {
        HintText.Text = text;
        HintText.Foreground = new SolidColorBrush(error ? Color.FromRgb(0xFF, 0x6B, 0x81) : Color.FromRgb(0x8A, 0x93, 0xBF));
    }

    void RefreshIdle()
    {
        if (_active) return;
        StatusText.Text = "Готов к запуску";
        if (IsTimerMode)
        {
            TimeText.Text = $"{_th.Value:00}:{_tm.Value:00}:00";
            var end = DateTime.Now.AddHours(_th.Value).AddMinutes(_tm.Value);
            SetHint(_th.Value == 0 && _tm.Value == 0 ? "Выберите время до выключения" : $"Сработает в {end:HH:mm}");
        }
        else
        {
            var end = NextClock();
            TimeText.Text = $"{end:HH:mm}";
            SetHint($"{(end.Date == DateTime.Today ? "Сегодня" : "Завтра")} в {end:HH:mm}, через {Span(end - DateTime.Now)}");
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
        var target = IsTimerMode ? now.AddHours(_th.Value).AddMinutes(_tm.Value) : NextClock();
        if ((target - now).TotalSeconds < 10)
        {
            SetHint("Укажите время больше нуля", true);
            return;
        }

        _start = now; _target = target; _intro = now;
        _fired.Clear(); _final = false; _active = true; _lastSec = -1;

        StatusText.Text = $"{(IsRestart ? "Перезагрузка" : "Выключение")} в {target:HH:mm}" +
                          (target.Date != now.Date ? " (завтра)" : "");
        SetHint("Таймер запущен");
        SetUi(true);
        Pulse();
    }

    void Cancel()
    {
        if (_final) Run("/a");
        _active = _final = false;
        DrawArc(0);
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
        _intro = _start.AddDays(-1);
        Run(IsRestart ? $"/r /t {FinalSeconds}" : $"/s /t {FinalSeconds}");

        StatusText.Text = "Завершение работы…";
        BtnGo.Content = "Отменить выключение";
        _tray.ShowBalloonTip(5000, "Shutdown Timer",
            $"Компьютер {(IsRestart ? "перезагрузится" : "выключится")} через {FinalSeconds} секунд. Нажмите «Отменить», чтобы остановить.",
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
        BtnGo.Content = active ? "Отменить" : "Запустить";
        BtnGo.Background = (Brush)FindResource(active ? "StopBrush" : "GoBrush");
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
        int secs = (int)Math.Ceiling(left);

        TimeText.Text = $"{secs / 3600:00}:{secs / 60 % 60:00}:{secs % 60:00}";

        double intro = Math.Min(1, (now - _intro).TotalMilliseconds / 900);
        intro = 1 - Math.Pow(1 - intro, 3);
        DrawArc(left / total * intro);

        if (secs != _lastSec)
        {
            _lastSec = secs;
            _tray.Text = $"{(_final ? "Завершение" : "Осталось")} {TimeText.Text}";
        }

        if (_final) return;

        foreach (var m in Marks)
        {
            if (_fired.Contains(m) || left > m) continue;
            _fired.Add(m);
            if (m < total - 1 && Reminders.IsChecked == true) Remind(m);
        }

        if (left <= 0) Fire();
    }

    void Remind(int seconds)
    {
        string t = seconds >= 60 ? $"{seconds / 60} мин" : $"{seconds} сек";
        _tray.ShowBalloonTip(6000, "Shutdown Timer",
            $"До {(IsRestart ? "перезагрузки" : "выключения")} осталось {t}", WF.ToolTipIcon.Info);
        Pulse();
    }

    // ---------------- Анимации ----------------

    void DrawArc(double f)
    {
        f = Math.Clamp(f, 0, 0.9999);
        if (f <= 0) { Arc.Data = Geometry.Empty; return; }

        double a = -Math.PI / 2 + 2 * Math.PI * f;
        var end = new Point(C + R * Math.Cos(a), C + R * Math.Sin(a));
        var fig = new PathFigure { StartPoint = new Point(C, C - R) };
        fig.Segments.Add(new ArcSegment(end, new Size(R, R), 0, f > 0.5, SweepDirection.Clockwise, true));
        Arc.Data = new PathGeometry(new[] { fig });
    }

    void Pulse()
    {
        var a = new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(260))
        { AutoReverse = true, EasingFunction = new SineEase() };
        RingScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        RingScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    void Breathe(bool on)
    {
        if (on)
            TimeText.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.55, TimeSpan.FromMilliseconds(1600))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
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
    readonly int _min, _max;
    int _v;

    public event Action? Changed;

    public int Value
    {
        get => _v;
        set { Set(value); Changed?.Invoke(); }
    }

    /// <summary>Установить значение без события Changed.</summary>
    public void Set(int value)
    {
        int n = _max - _min + 1;
        _v = ((value - _min) % n + n) % n + _min;
        _t.Text = _v.ToString("00");
        var a = new DoubleAnimation(1.18, 1, TimeSpan.FromMilliseconds(220))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var st = (ScaleTransform)_t.RenderTransform;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    public Stepper(string label, int min, int max, int init, Style soft)
    {
        _min = min; _max = max; _v = init;
        Margin = new Thickness(10, 0, 10, 0);
        Background = Brushes.Transparent;

        _t = new TextBlock
        {
            Text = init.ToString("00"), FontSize = 46, FontWeight = FontWeights.Light,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1),
            Margin = new Thickness(0, 2, 0, 0)
        };
        var cap = new TextBlock
        {
            Text = label, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0x7D, 0x86, 0xB3)), Margin = new Thickness(0, 0, 0, 6)
        };

        Children.Add(Btn("▲", 1, soft));
        Children.Add(_t);
        Children.Add(cap);
        Children.Add(Btn("▼", -1, soft));

        MouseWheel += (_, e) => { Value += e.Delta > 0 ? 1 : -1; e.Handled = true; };
    }

    RepeatButton Btn(string glyph, int delta, Style soft)
    {
        var b = new RepeatButton
        {
            Content = glyph, Style = soft, Width = 64, Height = 26, FontSize = 10, Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(0x1F, 255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xD0, 0xF5)),
            Delay = 350, Interval = 70, Focusable = false
        };
        b.Click += (_, _) => Value += delta;
        return b;
    }
}
