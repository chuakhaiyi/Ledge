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

        var themeManager = _services.GetRequiredService<ThemeManager>();
        themeManager.Initialize(_settingsStore);

        var hotkeyManager = _services.GetRequiredService<HotkeyManager>();
        hotkeyManager.Initialize(_settingsStore, _windowManager);

        var fullscreenDetector = _services.GetRequiredService<FullscreenDetector>();
        fullscreenDetector.Initialize(_windowManager);

        var systemTray = _services.GetRequiredService<SystemTrayService>();
        systemTray.Initialize(_windowManager);

        _settingsStore.ApplyStartupSettings();
        _windowManager.ShowDock();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _noteStore?.ForceSave();
        _settingsStore?.SaveAsync().Wait();
    }

    public static T GetService<T>() where T : notnull
        => ((App)Current)._services.GetRequiredService<T>();
}