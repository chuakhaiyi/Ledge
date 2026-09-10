namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class SearchBox : UserControl
{
    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(SearchBox),
            new PropertyMetadata(string.Empty, OnSearchTextChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(SearchBox),
            new PropertyMetadata("Search notes"));

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public event Action<string>? SearchTextChanged;

    public SearchBox()
    {
        InitializeComponent();
        SearchTextBox.ContextMenu = AppMenus.TextEditing(SearchTextBox);
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SearchBox box)
        {
            if (box.SearchTextBox.Text != (string)e.NewValue) box.SearchTextBox.Text = (string)e.NewValue;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchText = SearchTextBox.Text;
        SearchTextChanged?.Invoke(SearchText);
    }

    public void FocusSearch()
    {
        SearchTextBox.Focus();
        SearchTextBox.Select(0, 0);
        SearchTextBox.ScrollToHome();
    }
}
