namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using Ledge.Core.Models;

public partial class HotkeyEditor : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(HotkeyEditor),
            new PropertyMetadata(""));

    public static readonly DependencyProperty HotkeyProperty =
        DependencyProperty.Register(nameof(Hotkey), typeof(HotkeyBinding), typeof(HotkeyEditor),
            new PropertyMetadata(HotkeyBinding.Empty, OnHotkeyChanged));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public HotkeyBinding Hotkey
    {
        get => (HotkeyBinding)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    private bool _isListening = false;

    public HotkeyEditor()
    {
        InitializeComponent();
        HotkeyTextBox.Text = Hotkey.ToDisplayString();
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyEditor editor && editor.HotkeyTextBox != null)
        {
            editor.HotkeyTextBox.Text = ((HotkeyBinding)e.NewValue).ToDisplayString();
        }
    }

    private void HotkeyTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isListening = true;
        HotkeyTextBox.Text = "Press keys…";
        HotkeyTextBox.Background = (Brush)FindResource("AccentBrush");
        HotkeyTextBox.Foreground = Brushes.White;
    }

    private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isListening = false;
        HotkeyTextBox.Background = (Brush)FindResource("SurfaceBrush");
        HotkeyTextBox.Foreground = (Brush)FindResource("PrimaryTextBrush");
        HotkeyTextBox.Text = Hotkey.ToDisplayString();
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isListening) return;

        if (e.Key == Key.Escape)
        {
            _isListening = false;
            HotkeyTextBox.Text = Hotkey.ToDisplayString();
            HotkeyTextBox.Background = (Brush)FindResource("SurfaceBrush");
            HotkeyTextBox.Foreground = (Brush)FindResource("PrimaryTextBrush");
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 1;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 2;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 4;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 8;

        if (modifiers == 0)
        {
            return;
        }

        var newBinding = new HotkeyBinding(modifiers, KeyInterop.VirtualKeyFromKey(key));
        SetCurrentValue(HotkeyProperty, newBinding);
        _isListening = false;
        HotkeyTextBox.Text = newBinding.ToDisplayString();
        HotkeyTextBox.Background = (Brush)FindResource("SurfaceBrush");
        HotkeyTextBox.Foreground = (Brush)FindResource("PrimaryTextBrush");
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(HotkeyProperty, HotkeyBinding.Empty);
    }
}
