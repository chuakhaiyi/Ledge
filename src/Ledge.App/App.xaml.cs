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
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showLibraryEvent;
    private RegisteredWaitHandle? _showLibraryWait;
    private bool _openLibraryRequested;

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
        var startupOption = e.Args.FirstOrDefault(a => a.StartsWith("--configure-startup="));
        if (startupOption != null)
        {
            _settingsStore.StartWithWindows = startupOption == "--configure-startup=true";
            await _settingsStore.SaveAsync();
            Shutdown();
            return;
        }

        _instanceMutex = new Mutex(true, "Local\\Ledge", out var firstInstance);
        if (!firstInstance)
        {
            if (!e.Args.Contains("--background") && EventWaitHandle.TryOpenExisting("Local\\Ledge.ShowLibrary", out var existingEvent))
            { using (existingEvent) existingEvent.Set(); }
            // A second launch must not flush its older settings over the running app.
            _settingsStore = null;
            Shutdown();
            return;
        }
        _showLibraryEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Ledge.ShowLibrary");
        _showLibraryWait = ThreadPool.RegisterWaitForSingleObject(_showLibraryEvent, (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            _openLibraryRequested = true;
            _windowManager?.ShowLibrary();
        })), null, Timeout.Infinite, false);
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
        if (!e.Args.Contains("--background") || _openLibraryRequested) _windowManager.ShowLibrary();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        if (_isExiting) return;
        _isExiting = true;

        try
        {
            _noteStore?.ForceSave();
            _settingsStore?.SaveAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Your latest changes could not be saved.\n\n{exception.Message}",
                "Ledge — Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _showLibraryWait?.Unregister(null);
            _showLibraryEvent?.Dispose();
            _instanceMutex?.Dispose();
            _noteStore?.Dispose();
            _services.Dispose();
        }
    }

    public static T GetService<T>() where T : notnull
        => ((App)Current)._services.GetRequiredService<T>();
}
