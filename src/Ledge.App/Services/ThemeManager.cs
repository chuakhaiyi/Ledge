namespace Ledge.App.Services;

using System.Windows;
using Ledge.Core.Services;
using Ledge.Core.Models;

public sealed class ThemeManager
{
    private SettingsStore? _settingsStore;
    private ResourceDictionary? _lightDict;
    private ResourceDictionary? _darkDict;

    public void Initialize(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _settingsStore.PropertyChanged += OnSettingsChanged;

        _lightDict = new ResourceDictionary { Source = new Uri("pack://application:,,,/Ledge.App;component/Resources/Colors.Light.xaml", UriKind.Absolute) };
        _darkDict = new ResourceDictionary { Source = new Uri("pack://application:,,,/Ledge.App;component/Resources/Colors.Dark.xaml", UriKind.Absolute) };

        ApplyTheme(_settingsStore.Theme);
        SystemParameters.StaticPropertyChanged += OnSystemThemeChanged;
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsStore.Theme))
        {
            ApplyTheme(_settingsStore!.Theme);
        }
    }

    private void OnSystemThemeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "HighContrast" || _settingsStore?.Theme == AppTheme.System)
        {
            ApplyTheme(_settingsStore?.Theme ?? AppTheme.System);
        }
    }

    private void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        var dicts = app.Resources.MergedDictionaries;

        dicts.Remove(_lightDict!);
        dicts.Remove(_darkDict!);

        var effectiveTheme = theme == AppTheme.System ? GetSystemTheme() : theme;

        if (effectiveTheme == AppTheme.Dark)
        {
            dicts.Add(_darkDict!);
        }
        else
        {
            dicts.Add(_lightDict!);
        }
    }

    private static AppTheme GetSystemTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }
}