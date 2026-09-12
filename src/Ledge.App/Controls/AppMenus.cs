namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ledge.App.Services;

internal static class AppMenus
{
    public static ContextMenu TextEditing(TextBox target)
    {
        var menu = new ContextMenu();
        EnableOverlayDismissal(menu);
        target.PreviewMouseRightButtonDown += (_, e) => SelectSpellingWord(target, e.GetPosition(target));
        target.ContextMenuOpening += (_, args) => AddSpellingItems(menu, target);
        void Add(string label, RoutedUICommand command, string shortcut)
            => menu.Items.Add(new MenuItem { Header = label, Command = command, CommandTarget = target, InputGestureText = shortcut });
        Add("Undo", ApplicationCommands.Undo, "Ctrl+Z");
        Add("Redo", ApplicationCommands.Redo, "Ctrl+Y");
        menu.Items.Add(new Separator());
        Add("Cut", ApplicationCommands.Cut, "Ctrl+X");
        Add("Copy", ApplicationCommands.Copy, "Ctrl+C");
        Add("Paste", ApplicationCommands.Paste, "Ctrl+V");
        Add("Delete", ApplicationCommands.Delete, "Del");
        menu.Items.Add(new Separator());
        Add("Select all", ApplicationCommands.SelectAll, "Ctrl+A");
        return menu;
    }

    private static void AddSpellingItems(ContextMenu menu, TextBox target)
    {
        foreach (var item in menu.Items.OfType<MenuItem>().Where(item => item.Tag as string == "Spelling").ToList())
            menu.Items.Remove(item);
        foreach (var separator in menu.Items.OfType<Separator>().Where(separator => separator.Tag as string == "Spelling").ToList())
            menu.Items.Remove(separator);

        var index = target.CaretIndex;
        if (index < 0 || index >= target.Text.Length) return;
        var error = target.GetSpellingError(index);
        if (error == null) return;

        target.Select(target.GetSpellingErrorStart(index), target.GetSpellingErrorLength(index));
        var suggestions = error.Suggestions.Take(3).ToList();
        var insertAt = 0;
        foreach (var suggestion in suggestions)
        {
            var item = new MenuItem { Header = suggestion, Tag = "Spelling" };
            item.Click += (_, _) => error.Correct(suggestion);
            menu.Items.Insert(insertAt++, item);
        }

        var ignore = new MenuItem { Header = "Ignore", Tag = "Spelling" };
        ignore.Click += (_, _) => error.IgnoreAll();
        menu.Items.Insert(insertAt++, ignore);
        menu.Items.Insert(insertAt, new Separator { Tag = "Spelling" });
    }

    private static void SelectSpellingWord(TextBox target, Point point)
    {
        var index = target.GetCharacterIndexFromPoint(point, true);
        if (index >= 0 && index < target.Text.Length)
        {
            target.CaretIndex = index;
            var error = target.GetSpellingError(index);
            if (error != null)
                target.Select(target.GetSpellingErrorStart(index), target.GetSpellingErrorLength(index));
        }
    }

    public static ContextMenu NoteActions(Action delete)
    {
        var menu = new ContextMenu();
        EnableOverlayDismissal(menu);
        void Add(string label, Action action, bool destructive = false)
        {
            var item = new MenuItem { Header = label, Tag = destructive ? "Destructive" : null };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Add("Open dock", () => App.GetService<WindowManager>().FocusDock());
        Add("New note", () => App.GetService<WindowManager>().CreateNewNote());
        Add("All notes", () => App.GetService<WindowManager>().ShowLibrary());
        Add("Settings", () => App.GetService<WindowManager>().ShowSettings());
        menu.Items.Add(new Separator());
        Add("Delete note", delete, true);
        return menu;
    }

    private static void EnableOverlayDismissal(ContextMenu menu)
    {
        menu.StaysOpen = false;
        menu.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            menu.IsOpen = false;
            e.Handled = true;
        };
    }
}
