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
