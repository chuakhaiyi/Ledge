namespace Ledge.App.Services;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ledge.Core.Services;

public sealed class ToastManager
{
    private ToastNotificationWindow? _currentToast;

    public void ShowDeleteToast(NoteStore store, Action onUndo)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            HideCurrentToast();

            if (store.UndoRemaining == TimeSpan.Zero) return;
            var toast = new ToastNotificationWindow(() => store.UndoRemaining)
            {
                Message = "Deleted. 10s to undo.",
                Owner = Application.Current.MainWindow
            };
            _currentToast = toast;
            toast.UndoRequested += onUndo;
            toast.Closed += (_, _) => { if (ReferenceEquals(_currentToast, toast)) _currentToast = null; };
            toast.Show();
        });
    }

    public void HideCurrentToast()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_currentToast != null)
            {
                _currentToast.Close();
                _currentToast = null;
            }
        });
    }
}

public sealed class ToastNotificationWindow : Window
{
    private readonly Func<TimeSpan> _remaining;
    private readonly ScaleTransform _countdown = new(1, 1);

    public double CountdownFraction => _countdown.ScaleX;

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ToastNotificationWindow),
            new PropertyMetadata(""));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public event Action? UndoRequested;

    public ToastNotificationWindow(Func<TimeSpan> remaining)
    {
        _remaining = remaining;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            MinWidth = 280,
            MaxWidth = 400,
            Effect = (System.Windows.Media.Effects.Effect)FindResource("ShadowEffect"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 24)
        };
        border.SetResourceReference(Border.BackgroundProperty, "AccentBrush");

        var grid = new Grid { Margin = new Thickness(16, 12, 16, 12) };
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
        var content = new Grid();
        // Clip in the container's coordinates so depletion never scales its corners.
        content.SizeChanged += (_, _) => content.Clip = new RectangleGeometry(
            new Rect(content.RenderSize), border.CornerRadius.BottomLeft, border.CornerRadius.BottomRight);
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });
        content.Children.Add(grid);
        var progress = new Border
        {
            Name = "UndoCountdown", Background = Brushes.White, Opacity = 0.8,
            RenderTransform = _countdown
        };
        System.Windows.Automation.AutomationProperties.SetName(progress, "Time remaining to undo deletion");
        Grid.SetRow(progress, 1);
        content.Children.Add(progress);
        border.Child = content;

        Content = border;

        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering += UpdateCountdown;
            UpdateCountdown(null, EventArgs.Empty);
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

    private void UpdateCountdown(object? sender, EventArgs e)
    {
        _countdown.ScaleX = Math.Clamp(_remaining().TotalSeconds / NoteStore.UndoWindow.TotalSeconds, 0, 1);
        if (_countdown.ScaleX == 0) Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        CompositionTarget.Rendering -= UpdateCountdown;
        base.OnClosed(e);
        UndoRequested = null;
    }
}
