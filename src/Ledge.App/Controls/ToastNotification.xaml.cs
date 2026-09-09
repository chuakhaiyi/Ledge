namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

public partial class ToastNotification : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ToastNotification),
            new PropertyMetadata(""));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public event Action? UndoRequested;

    public ToastNotification()
    {
        InitializeComponent();
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 100,
            Duration = TimeSpan.FromSeconds(0.2),
            EasingFunction = (IEasingFunction)FindResource("CollapseEasing")
        };
        animation.Completed += (_, _) => Visibility = Visibility.Collapsed;
        SlideTransform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        UndoRequested?.Invoke();
        Hide();
    }
}