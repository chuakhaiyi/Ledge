namespace Ledge.App;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Ledge.App.Services;
using Ledge.Core.Services;
using Ledge.Core.Models;

public partial class App : Application
{
    private readonly ServiceProvider _services;
    private NoteStore? _noteStore;
    private SettingsStore? _settingsStore;
    private FilePersistence? _persistence;
    private WindowManager? _windowManager;
    private HotkeyManager? _hotkeyManager;
    private ThemeManager? _themeManager;
    private SystemTrayService? _systemTray;
    private bool _isExiting;

    public App()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<HotkeyManager>();
        collection.AddSingleton<ThemeManager>();
        collection.AddSingleton<FullscreenDetector>();
        collection.AddSingleton<WindowManager>();
        collection.AddSingleton<SystemTrayService>();
        _services = collection.BuildServiceProvider();
    }

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _persistence = new FilePersistence(Settings.Default);
        var data = await _persistence.LoadAsync();

        _settingsStore = new SettingsStore(_persistence, data.Settings);
        _noteStore = new NoteStore(_persistence);
        await _noteStore.LoadAsync();

        _windowManager = _services.GetRequiredService<WindowManager>();
        _windowManager.Initialize(_noteStore, _settingsStore);

        _themeManager = _services.GetRequiredService<ThemeManager>();
        _themeManager.Initialize(_settingsStore);

        _hotkeyManager = _services.GetRequiredService<HotkeyManager>();
        _hotkeyManager.Initialize(_settingsStore, _windowManager);

        var fullscreenDetector = _services.GetRequiredService<FullscreenDetector>();
        fullscreenDetector.Initialize(_windowManager);

        _systemTray = _services.GetRequiredService<SystemTrayService>();
        _systemTray.Initialize(_windowManager);

        _settingsStore.ApplyStartupSettings();
        _windowManager.ShowDock();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        if (_isExiting) return;
        _isExiting = true;

        _systemTray?.Dispose();
        _hotkeyManager?.Dispose();
        _themeManager?.Dispose();
        _noteStore?.ForceSave();
        _settingsStore?.SaveAsync().Wait();
        _services.Dispose();
    }

    public static T GetService<T>() where T : notnull
        => ((App)Current)._services.GetRequiredService<T>();
}