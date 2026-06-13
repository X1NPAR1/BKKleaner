using System.ComponentModel;
using System.Windows;
using BKKleaner.Localization;
using BKKleaner.Services;
using BKKleaner.ViewModels;

namespace BKKleaner.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settings;
    private bool _exitRequested;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = viewModel;

        viewModel.ShowRequested += (_, _) => RestoreFromTray();
        viewModel.ExitRequested += (_, _) =>
        {
            _exitRequested = true;
            Tray.Dispose();
            Application.Current.Shutdown();
        };

        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Loaded += (_, _) => LoadTrayIcon();
    }

    private void LoadTrayIcon()
    {
        try
        {
            var icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "logo.ico");
            if (System.IO.File.Exists(icoPath))
                Tray.Icon = new System.Drawing.Icon(icoPath);
        }
        catch
        {
            // Non-fatal: tray works without a custom glyph.
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.Current.MinimizeToTray)
            Hide();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested || !_settings.Current.CloseToTray) return;

        // Close button minimizes to tray instead of exiting.
        e.Cancel = true;
        Hide();
        Tray.ShowBalloonTip("BKKleaner", Loc.Instance["tray.minimized"],
            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }
}
