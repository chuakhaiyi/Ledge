namespace Ledge.App.Windows;

using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Ledge.App.Services;
using Ledge.Core.Services;
using Ledge.Core.Models;

public partial class LibraryWindow : Window
{
    private readonly NoteStore _noteStore;
    private readonly SettingsStore _settingsStore;
    private readonly ObservableCollection<NoteRow> _allRows = [];
    private readonly List<ListView> _sectionLists = [];
    private ICollectionView? _pinnedView;
    private ICollectionView? _notesView;
    private ICollectionView? _archivedView;
    private string _currentSearch = "";
    private NoteSortMode _sortMode = NoteSortMode.ModifiedDesc;
    private bool _showArchived = false;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };

    public NoteSortMode SortMode
    {
        get => _sortMode;
        set
        {
            if (_sortMode != value)
            {
                _sortMode = value;
                RefreshFilters();
            }
        }
    }

    public LibraryWindow(NoteStore noteStore, SettingsStore settingsStore)
    {
        _noteStore = noteStore;
        _settingsStore = settingsStore;
        InitializeComponent();
        SettingsHost.Content = new Ledge.App.Controls.SettingsPanel(settingsStore);

        _noteStore.PropertyChanged += OnNotesChanged;
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RefreshList(); };
        Closed += (_, _) => { _previewTimer.Stop(); _noteStore.PropertyChanged -= OnNotesChanged; };
    }

    private void OnNotesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NoteStore.Notes)) return;
        Dispatcher.Invoke(() =>
        {
            var notes = _noteStore.Notes;
            var immediate = notes.Count != _allRows.Count || notes.Any(n => !_allRows.Any(r => r.Note.Id == n.Id && r.Note.Color == n.Color && r.Note.Pinned == n.Pinned && r.Note.Archived == n.Archived));
            if (immediate) { _previewTimer.Stop(); RefreshList(); }
            else if (notes.Any(n => _allRows.Any(r => r.Note.Id == n.Id && r.Note.Text != n.Text)))
            { _previewTimer.Stop(); _previewTimer.Start(); }
        });
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Populate sort mode combo box
        SortComboBox.ItemsSource = new[]
        {
            new { Value = NoteSortMode.ModifiedDesc, Label = "Last edited" },
            new { Value = NoteSortMode.CreatedDesc, Label = "Newest first" },
            new { Value = NoteSortMode.Color, Label = "Color" },
            new { Value = NoteSortMode.Alphabetical, Label = "A to Z" }
        };
        SortComboBox.DisplayMemberPath = "Label";
        SortComboBox.SelectedValuePath = "Value";
        SortComboBox.SelectedValue = _sortMode;
        
        RefreshList();
        if (LibraryContent.IsVisible) SearchBox.FocusSearch();
    }

    public void ShowSettings()
    {
        LibraryContent.Visibility = Visibility.Collapsed;
        SettingsHost.Visibility = Visibility.Visible;
        SettingsNavigation.IsChecked = true;
        SettingsNavigation.Focus();
    }

    public void ShowNotes()
    {
        SettingsHost.Visibility = Visibility.Collapsed;
        LibraryContent.Visibility = Visibility.Visible;
        NotesNavigation.IsChecked = true;
        SearchBox.FocusSearch();
    }

    private void NotesNavigation_Click(object sender, RoutedEventArgs e) => ShowNotes();
    private void SettingsNavigation_Click(object sender, RoutedEventArgs e) => ShowSettings();

    private void RefreshList()
    {
        var notes = _noteStore.Notes;
        var sameSections = notes.Count == _allRows.Count && notes.All(n => _allRows.Any(r => r.Note.Id == n.Id && r.Note.Pinned == n.Pinned && r.Note.Archived == n.Archived));
        if (sameSections && _notesView != null && string.IsNullOrEmpty(_currentSearch))
        {
            foreach (var row in _allRows) row.Update(notes.Single(n => n.Id == row.Note.Id));
            _pinnedView?.Refresh(); _notesView.Refresh(); _archivedView?.Refresh();
            return;
        }
        _allRows.Clear();

        foreach (var note in _noteStore.Notes)
        {
            _allRows.Add(new NoteRow(note));
        }

        BuildViews();
        BuildUI();
    }

    private void BuildViews()
    {
        _pinnedView = new ListCollectionView(_allRows.Where(r => r.Note.Pinned && !r.Note.Archived).ToList());
        _notesView = new ListCollectionView(_allRows.Where(r => !r.Note.Pinned && !r.Note.Archived).ToList());
        _archivedView = new ListCollectionView(_allRows.Where(r => r.Note.Archived).ToList());

        ApplyFilterAndSort(_pinnedView);
        ApplyFilterAndSort(_notesView);
        ApplyFilterAndSort(_archivedView);
    }

    private void ApplyFilterAndSort(ICollectionView view)
    {
        view.Filter = string.IsNullOrEmpty(_currentSearch)
            ? null
            : o => o is NoteRow row && row.Note.Text.ToLowerInvariant().Contains(_currentSearch);

        view.SortDescriptions.Clear();
        switch (_sortMode)
        {
            case NoteSortMode.ModifiedDesc:
                view.SortDescriptions.Add(new SortDescription("Note.ModifiedAt", ListSortDirection.Descending));
                break;
            case NoteSortMode.CreatedDesc:
                view.SortDescriptions.Add(new SortDescription("Note.CreatedAt", ListSortDirection.Descending));
                break;
            case NoteSortMode.Color:
                view.SortDescriptions.Add(new SortDescription("Note.Color", ListSortDirection.Ascending));
                break;
            case NoteSortMode.Alphabetical:
                view.SortDescriptions.Add(new SortDescription("Note.Text", ListSortDirection.Ascending));
                break;
        }
    }

    private void BuildUI()
    {
        _sectionLists.Clear();
        NotesPanel.Children.Clear();
        if (_allRows.Count == 0 || !_allRows.Any(r => r.Note.Text.ToLowerInvariant().Contains(_currentSearch)))
        {
            NotesPanel.Children.Add(new TextBlock
            {
                Text = _allRows.Count == 0 ? "No notes yet. Create a note to get started." : "No notes match your search.",
                Style = (Style)FindResource("SecondaryText"),
                Margin = new Thickness(0, 24, 0, 0)
            });
        }

        if (_pinnedView is not null && _pinnedView.Cast<NoteRow>().Any())
        {
            AddSection("Pinned", _pinnedView);
        }

        if (_notesView is not null && _notesView.Cast<NoteRow>().Any())
        {
            AddSection("Notes", _notesView);
        }

        if (_archivedView is not null && _archivedView.Cast<NoteRow>().Any())
        {
            var header = AddSection("Archived", _archivedView, isCollapsed: !_showArchived);
            header.Collapsed += (_, _) => _showArchived = false;
            header.Expanded += (_, _) => _showArchived = true;
        }
    }

    private Expander AddSection(string title, ICollectionView view, bool isCollapsed = false)
    {
        var count = view.Cast<NoteRow>().Count();
        var expander = new Expander
        {
            Header = new TextBlock
            {
                Text = title + (title == "Archived" ? $" ({count})" : ""),
                Style = (Style)FindResource("SectionHeaderStyle"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            },
            IsExpanded = !isCollapsed,
            Margin = new Thickness(0, 0, 0, 22)
        };

        var listView = new ListView
        {
            Style = (Style)FindResource("NotesListStyle"),
            ItemsSource = view,
            MaxHeight = 360,
            ItemTemplate = (DataTemplate)FindResource("NoteRowTemplate"),
            BorderThickness = new Thickness(0)
        };
        _sectionLists.Add(listView);
        listView.SelectionMode = SelectionMode.Single;
        listView.SelectionChanged += (_, e) =>
        {
            if (e.AddedItems.Count == 0) return;
            foreach (var other in _sectionLists.Where(other => other != listView)) other.SelectedItem = null;
        };
        listView.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(listView, source) is ListViewItem item)
            {
                item.IsSelected = true;
                item.Focus();
            }
        };
        // ContextMenuService must find a menu before its first opening event.
        listView.ContextMenu = new ContextMenu();

        listView.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && listView.SelectedItem is NoteRow row) OpenNote(row.Note);
        };
        listView.MouseDoubleClick += (_, e) =>
        {
            if (listView.SelectedItem is NoteRow row)
            {
                OpenNote(row.Note);
            }
        };

        listView.ContextMenuOpening += (_, e) =>
        {
            if (listView.SelectedItem is not NoteRow row) { e.Handled = true; return; }
            listView.ContextMenu.ItemsSource = new object[]
            {
                new MenuItem { Header = row.Note.Pinned ? "Unpin" : "Pin", Command = new RelayCommand(() => TogglePin(listView)) },
                new MenuItem { Header = row.Note.Archived ? "Restore" : "Archive", Command = new RelayCommand(() => ToggleArchive(listView)) },
                new MenuItem { Header = "Export…", Command = new RelayCommand(() => ExportNote(row)) },
                new MenuItem { Header = "Copy text", Command = new RelayCommand(() => CopyText(listView)) },
                new Separator(),
                new MenuItem { Header = "Delete", Tag = "Destructive", Command = new RelayCommand(() => DeleteNote(listView)) }
            };
        };

        expander.Content = listView;
        NotesPanel.Children.Add(expander);
        return expander;
    }

    private void SearchBox_SearchTextChanged(string query)
    {
        _currentSearch = query?.ToLowerInvariant() ?? "";
        RefreshFilters();
    }

    private void RefreshFilters()
    {
        if (_pinnedView == null || _notesView == null || _archivedView == null) return;
        ApplyFilterAndSort(_pinnedView);
        ApplyFilterAndSort(_notesView);
        ApplyFilterAndSort(_archivedView);
        BuildUI(); // Rebuild to update Archived count in header
    }

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        var windowManager = App.GetService<WindowManager>();
        windowManager.CreateNewNote();
    }

    private void OpenNote(Note note)
    {
        var windowManager = App.GetService<WindowManager>();
        windowManager.ShowNoteWindow(note);
    }

    private void TogglePin(ListView listView)
    {
        if (listView.SelectedItem is NoteRow row)
        {
            var current = _noteStore.Notes.Single(n => n.Id == row.Note.Id);
            _noteStore.Update(current with { Pinned = !current.Pinned });
        }
    }

    private void ToggleArchive(ListView listView)
    {
        if (listView.SelectedItem is NoteRow row)
        {
            if (row.Note.Archived)
            {
                _noteStore.Unarchive(_noteStore.Notes.Single(n => n.Id == row.Note.Id));
            }
            else
            {
                _noteStore.Archive(_noteStore.Notes.Single(n => n.Id == row.Note.Id));
            }
        }
    }

    private void DeleteNote(ListView listView)
    {
        if (listView.SelectedItem is NoteRow row)
        {
            _noteStore.Delete(row.Note);
        }
    }

    private void CopyText(ListView listView)
    {
        if (listView.SelectedItem is NoteRow row)
        {
            Clipboard.SetText(_noteStore.Notes.Single(n => n.Id == row.Note.Id).Text);
        }
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (SortComboBox.SelectedValue is NoteSortMode mode)
    {
        _sortMode = mode;
        RefreshFilters();
    }
}

private void ExportNote(NoteRow row)
    {
        var note = _noteStore.Notes.Single(n => n.Id == row.Note.Id);
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|JSON files (*.json)|*.json",
            FileName = $"{GetSafeFileName(note.Text.Split('\n')[0])}.txt",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                if (dialog.FilterIndex == 1) // txt
                {
                    File.WriteAllText(dialog.FileName, note.Text);
                }
                else // json
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(note, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dialog.FileName, json);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private static string GetSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(name.Where(c => !invalid.Contains(c))).Trim();
        return string.IsNullOrEmpty(safe) ? "Note" : safe[..Math.Min(50, safe.Length)];
    }

    public void ShowArchive()
    {
        ShowNotes();
        _showArchived = true;
        RefreshList();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.FocusSearch();
            e.Handled = true;
        }
    }
}

public enum NoteSortMode
{
    ModifiedDesc,
    CreatedDesc,
    Color,
    Alphabetical
}

public sealed class NoteRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Note Note { get; private set; }
    public string PreviewText => Note.Text.Split('\n')[0].TrimEnd('\r');
    public string MetaText => $"{Note.Color} · {GetRelativeTime(Note.ModifiedAt)}";
    public string ColorBrush => Note.Color.ToHex();

    public NoteRow(Note note)
    {
        Note = note;
    }

    public void Update(Note note)
    {
        var old = Note;
        if (old == note) return;
        var preview = PreviewText;
        var meta = MetaText;
        Note = note;
        if (old.Text != note.Text || old.Color != note.Color || old.Pinned != note.Pinned || old.Archived != note.Archived) OnPropertyChanged(nameof(Note));
        if (preview != PreviewText) OnPropertyChanged(nameof(PreviewText));
        if (meta != MetaText) OnPropertyChanged(nameof(MetaText));
        if (old.Color != note.Color) OnPropertyChanged(nameof(ColorBrush));
    }

    private static string GetRelativeTime(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dt.ToString("MMM d");
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    public event EventHandler? CanExecuteChanged;
    public RelayCommand(Action execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
