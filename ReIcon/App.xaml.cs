using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReIcon.Models;
using ReIcon.Services;

namespace ReIcon;

public partial class App : Application
{
    /// <summary>
    /// Крестик на дочернем окне вызывает <see cref="Application.Shutdown"/> до того, как
    /// <see cref="System.Windows.Threading.Dispatcher.HasShutdownStarted"/> станет true — нужен явный признак для MainWindow.
    /// </summary>
    internal static bool ExitRequestedFromChildTitleBar;

    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateWaitRegistration;

    protected override void OnStartup(StartupEventArgs e)
    {
        var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "ReIcon_ActivateMainWindow");

        _mutex = new Mutex(true, "ReIcon_SingleInstance_Mutex", out var createdNew);
        if (!createdNew)
        {
            activateEvent.Set();
            activateEvent.Dispose();
            Shutdown();
            return;
        }

        _activateEvent = activateEvent;
        _activateWaitRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, timedOut) =>
            {
                if (timedOut)
                    return;
                Dispatcher.BeginInvoke(
                    () =>
                    {
                        if (MainWindow is MainWindow mw)
                        {
                            mw.Show();
                            if (mw.WindowState == WindowState.Minimized)
                                mw.WindowState = WindowState.Normal;
                            mw.Activate();
                        }
                    },
                    System.Windows.Threading.DispatcherPriority.Normal);
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        base.OnStartup(e);

        ThemeService.Apply(UiTheme.Dark);

        var settings = SettingsStorage.Load();
        var lang = string.IsNullOrWhiteSpace(settings.Ui.Language)
            ? AppLanguageCatalog.ResolveDefaultLanguage()
            : AppLanguageCatalog.Normalize(settings.Ui.Language);
        LocalizationService.Apply(lang);

        using var icon = AppIconFactory.CreateApplicationIcon(UiTheme.Dark);
        var wpfIcon = ToImageSource(icon);

        MainWindow = new MainWindow { Icon = wpfIcon };
        MainWindow.Show();
    }

    internal void RefreshIconsForTheme(UiTheme theme)
    {
        _ = theme;
        if (MainWindow is MainWindow mw)
        {
            using var ico = AppIconFactory.CreateApplicationIcon(UiTheme.Dark);
            mw.Icon = ToImageSource(ico);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_activateWaitRegistration != null && _activateEvent != null)
        {
            _activateWaitRegistration.Unregister(_activateEvent);
            _activateWaitRegistration = null;
        }

        _activateEvent?.Dispose();
        _activateEvent = null;

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // ignored
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static ImageSource ToImageSource(Icon icon)
    {
        return Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
    }
}
