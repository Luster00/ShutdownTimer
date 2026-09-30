using System;
using System.Threading;
using System.Windows;

namespace ShutdownTimer;

public partial class App : Application
{
    // "Local\" — имя действует в пределах сеанса пользователя Windows
    const string MutexName = @"Local\ShutdownTimer.SingleInstance";
    const string ShowEventName = @"Local\ShutdownTimer.ShowWindow";

    Mutex? _mutex;
    bool _ownsMutex;
    EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out _ownsMutex);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (!_ownsMutex)
        {
            // Копия уже запущена: просим её показать окно и завершаемся, не создавая окна и значка в трее.
            _showEvent.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var window = new MainWindow();
        window.Show();

        // Фоновый поток ждёт сигнала от повторных запусков и открывает окно из трея.
        var listener = new Thread(() =>
        {
            try
            {
                while (_showEvent!.WaitOne())
                    Dispatcher.BeginInvoke(new Action(window.ShowFromTray));
            }
            catch (ObjectDisposedException) { /* приложение закрывается */ }
        })
        {
            IsBackground = true,
            Name = "SingleInstanceListener"
        };
        listener.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
