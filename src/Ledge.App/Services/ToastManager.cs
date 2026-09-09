namespace Ledge.App.Services;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Ledge.Core.Models;

public sealed class ToastManager
{
    private readonly DispatcherTimer _autoHideTimer;
    private ToastNotificationWindow? _currentToast;

    public ToastManager()
    {
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autoHideTimer.Tick += (_, _) => HideCurrentToast();
    }

    public void ShowDeleteToast(Note note, Action onUndo)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            HideCurrentToast();

            _currentToast = new ToastNotificationWindow
            {
                Message = "Deleted. 10s to undo.",
                Owner = Application.Current.MainWindow
            };
            _currentToast.UndoRequested += () => onUndo();
            _currentToast.Show();
            _currentToast.Activate();

            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        });
    }

    public void HideCurrentToast()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_currentToast != null)
            {
                _currentToast.UndoRequested -= null;
                _currentToast.Close();
                _currentToast = null;
                _autoHideTimer.Stop();
            }
        });
    }
}

public sealed class ToastNotificationWindow : Window
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ToastNotificationWindow),
            new PropertyMetadata(""));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public event Action? UndoRequested;

    public ToastNotificationWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var border = new Border
        {
            Background = (Brush)FindResource("SuccessBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            MinWidth = 280,
            MaxWidth = 400,
            Effect = (System.Windows.Media.Effects.Effect)FindResource("ShadowEffect"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 24)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var messageText = new TextBlock
        {
            Foreground = Brushes.White,
            FontFamily = (FontFamily)FindResource("UIFont"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        BindingOperations.SetBinding(messageText, TextBlock.TextProperty, new Binding(nameof(Message)) { Source = this });
        Grid.SetColumn(messageText, 1);

        var undoButton = new Button
        {
            Content = "Undo",
            Foreground = Brushes.White,
            FontWeight = FontWeights.Medium,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        undoButton.Click += (_, _) => { UndoRequested?.Invoke(); Close(); };
        Grid.SetColumn(undoButton, 2);

        grid.Children.Add(messageText);
        grid.Children.Add(undoButton);
        border.Child = grid;

        Content = border;

        Loaded += (_, _) =>
        {
            // Position at bottom center of screen
            var screen = SystemParameters.WorkArea;
            Left = screen.Left + (screen.Width - ActualWidth) / 2;
            Top = screen.Bottom - ActualHeight - 24;

            // Slide up animation
            var animation = new DoubleAnimation
            {
                From = 100,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var transform = new TranslateTransform();
            border.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.YProperty, animation);
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        UndoRequested = null;
    }
}