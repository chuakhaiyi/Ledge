namespace Ledge.App.Windows;

using System.Windows;
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
    private ICollectionView? _pinnedView;
    private ICollectionView? _notesView;
    private ICollectionView? _archivedView;
    private string _currentSearch = "";
    private NoteSortMode _sortMode = NoteSortMode.ModifiedDesc;
    private bool _showArchived = false;

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

        _noteStore.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(NoteStore.Notes)
                or nameof(NoteStore.VisibleNotes)
                or nameof(NoteStore.PinnedNotes)
                or nameof(NoteStore.ArchivedNotes))
            {
                RefreshList();
            }
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshList();
        SearchBox.FocusSearch();
    }

    private void RefreshList()
    {
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
                view.SortDescriptions.Add(new SortDescription(nameof(NoteRow.Note.ModifiedAt), ListSortDirection.Descending));
                break;
            case NoteSortMode.CreatedDesc:
                view.SortDescriptions.Add(new SortDescription(nameof(NoteRow.Note.CreatedAt), ListSortDirection.Descending));
                break;
            case NoteSortMode.Color:
                view.SortDescriptions.Add(new SortDescription(nameof(NoteRow.Note.Color), ListSortDirection.Ascending));
                break;
            case NoteSortMode.Alphabetical:
                view.SortDescriptions.Add(new SortDescription(nameof(NoteRow.Note.Text), ListSortDirection.Ascending));
                break;
        }
    }

    private void BuildUI()
    {
        NotesPanel.Children.Clear();

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
                Foreground = (Brush)FindResource("SecondaryTextBrush")
            },
            IsExpanded = !isCollapsed,
            Margin = new Thickness(0, 0, 0, 22)
        };

        var listView = new ListView
        {
            Style = (Style)FindResource("NotesListStyle"),
            ItemsSource = view,
            ItemTemplate = (DataTemplate)FindResource("NoteRowTemplate"),
            BorderThickness = new Thickness(0)
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
            if (listView.SelectedItem is not NoteRow row) return;
            listView.ContextMenu = new ContextMenu
            {
                ItemsSource = new object[]
                {
                    new MenuItem { Header = row.Note.Pinned ? "Unpin" : "Pin", Command = new RelayCommand(() => TogglePin(listView)) },
                    new MenuItem { Header = row.Note.Archived ? "Restore" : "Archive", Command = new RelayCommand(() => ToggleArchive(listView)) },
                    new MenuItem { Header = "Delete", Command = new RelayCommand(() => DeleteNote(listView)) },
                    new Separator(),
                    new MenuItem { Header = "Copy Text", Command = new RelayCommand(() => CopyText(listView)) },
                    new MenuItem { Header = "Export…", Command = new RelayCommand(() => ExportNote(row)) }
                }
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
        ApplyFilterAndSort(_pinnedView!);
        ApplyFilterAndSort(_notesView!);
        ApplyFilterAndSort(_archivedView!);
        BuildUI(); // Rebuild to update Archived count in header
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
            _noteStore.Update(row.Note with { Pinned = !row.Note.Pinned });
        }
    }

    private void ToggleArchive(ListView listView)
    {
        if (listView.SelectedItem is NoteRow row)
        {
            if (row.Note.Archived)
            {
                _noteStore.Unarchive(row.Note);
            }
            else
            {
                _noteStore.Archive(row.Note);
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
            Clipboard.SetText(row.Note.Text);
        }
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (e.AddedItems.Count > 0 && e.AddedItems[0] is NoteSortMode mode)
    {
        _sortMode = mode;
        RefreshFilters();
    }
}

private void ExportNote(NoteRow row)
    {
        var note = row.Note;
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
        return string.Concat(name.Where(c => !invalid.Contains(c))).Trim().Substring(0, Math.Min(50, name.Length));
    }

    public void ShowArchive()
    {
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

    public Note Note { get; }
    public string PreviewText => Note.Text.Split('\n')[0];
    public string MetaText => $"{Note.Color} · {GetRelativeTime(Note.ModifiedAt)}";
    public string ColorBrush => Note.Color.ToHex();

    public NoteRow(Note note)
    {
        Note = note;
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