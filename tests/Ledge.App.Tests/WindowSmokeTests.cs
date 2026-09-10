namespace Ledge.App.Tests;

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ledge.App.Controls;
using Ledge.App.Windows;
using Ledge.App.Services;
using Hardcodet.Wpf.TaskbarNotification;
using System.Reflection;
using Ledge.Core.Models;
using Ledge.Core.Services;
using Xunit;

public class WindowSmokeTests
{
    [Fact]
    public void SettingsNotesDockAndShutdownWorkOnTheUiThread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "Ledge-ui-test-" + Guid.NewGuid());
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                foreach (var name in new[] { "Colors.Light", "Styles", "Templates" })
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Ledge.App;component/Resources/{name}.xaml") });
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var persistence = new FilePersistence(Settings.Default, directory);
                var settings = new SettingsStore(persistence, Settings.Default);
                using var store = new NoteStore(persistence);
                var note = store.Add("Call the vet\nBook the annual check-up for Friday.", NoteColor.Moss);
                store.Add("Weekend groceries\nCoffee, tomatoes, sourdough", NoteColor.Butter);
                store.Add("An idea for later\nKeep the first version small.", NoteColor.Clay);
                var settingsWindow = new LibraryWindow(store, settings);
                settingsWindow.Show();
                settingsWindow.ShowSettings();
                settingsWindow.UpdateLayout();
                var tabs = Descendants<TabControl>(settingsWindow).Single();
                foreach (TabItem tab in tabs.Items)
                {
                    tabs.SelectedItem = tab;
                    settingsWindow.UpdateLayout();
                }
                tabs.SelectedIndex = 2;
                settingsWindow.UpdateLayout();
                Assert.Equal(4, Descendants<HotkeyEditor>(settingsWindow).Count());
                tabs.SelectedIndex = 0;
                settingsWindow.UpdateLayout();
                Pump(100);
                var combo = Descendants<ComboBox>(tabs).First(c => c.IsVisible);
                combo.IsDropDownOpen = true;
                settingsWindow.UpdateLayout();
                Assert.True(((System.Windows.Controls.Primitives.Popup)combo.Template.FindName("PART_Popup", combo)).IsOpen);
                combo.IsDropDownOpen = false;
                combo.SelectedItem = DockEdge.Left;
                Assert.Equal(DockEdge.Left, settings.DockEdge);
                Capture(settingsWindow, "settings-light");
                var dark = new ResourceDictionary { Source = new Uri("pack://application:,,,/Ledge.App;component/Resources/Colors.Dark.xaml") };
                app.Resources.MergedDictionaries.Add(dark);
                Assert.Equal(((SolidColorBrush)app.FindResource("BackgroundBrush")).Color, ((SolidColorBrush)settingsWindow.Background).Color);
                Capture(settingsWindow, "settings-dark");
                app.Resources.MergedDictionaries.Remove(dark);
                settingsWindow.Close();
                settingsWindow = new LibraryWindow(store, settings);
                settingsWindow.Show();
                settingsWindow.Close();

                store.Pin(note);
                note = store.Notes.Single(n => n.Id == note.Id);
                var dock = new DockWindow(store, settings);
                dock.Show();
                var noteWindow = new NoteWindow(note, store, settings);
                noteWindow.Show();
                var editor = Descendants<NoteEditor>(noteWindow).Single();
                var text = Descendants<TextBox>(editor).Single();
                text.Text = "Updated note\nThis text must survive closing and shutdown.";
                Assert.Equal(text.Text, store.Notes.Single(n => n.Id == note.Id).Text);
                var library = new LibraryWindow(store, settings);
                library.Show();
                Pump();
                var retainedTabs = Descendants<DockTab>(dock).ToArray();
                var retainedLists = Descendants<ListView>(library).ToArray();
                foreach (var list in retainedLists.Where(l => l.Items.Count > 0))
                {
                    Assert.NotNull(list.ContextMenu);
                    list.SelectedIndex = 0;
                    Assert.Single(retainedLists.Where(l => l.SelectedItem != null));
                    var originalMenu = list.ContextMenu;
                    var opening = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { list, true }, null)!;
                    list.RaiseEvent(opening);
                    Assert.False(opening.Handled);
                    Assert.Same(originalMenu, list.ContextMenu);
                    Assert.Equal("Delete", ((MenuItem)list.ContextMenu!.Items[^1]).Header);
                    Assert.Equal("Destructive", ((MenuItem)list.ContextMenu.Items[^1]).Tag);
                    list.ContextMenu.IsOpen = true;
                    Pump(100);
                    Capture(list.ContextMenu, "list-menu");
                    list.ContextMenu.IsOpen = false;
                }
                var retainedSource = dock.CollapsedNotes;
                var retainedFill = ((Border)editor.FindName("NoteSurface")).Background;
                var unloads = 0;
                foreach (var tab in retainedTabs) tab.Unloaded += (_, _) => unloads++;
                var beforeTyping = retainedTabs.First(t => t.Note!.Id == note.Id).Note!.Text;
                text.Text = "Updated note\r\nA Windows line ending must not add a blank title line.";
                for (var i = 0; i < 20; i++)
                {
                    text.AppendText("x");
                    Assert.Equal(text.Text, store.Notes.Single(n => n.Id == note.Id).Text);
                    Assert.Same(retainedSource, dock.CollapsedNotes);
                    Assert.Equal(beforeTyping, retainedTabs.First(t => t.Note!.Id == note.Id).Note!.Text);
                }
                for (var i = 0; i < 20; i++) text.Text = text.Text[..^1];
                Assert.Same(retainedFill, ((Border)editor.FindName("NoteSurface")).Background);
                Pump();
                Assert.Equal(0, unloads);
                Assert.Equal(retainedTabs, Descendants<DockTab>(dock));
                Assert.Equal(retainedLists, Descendants<ListView>(library));
                Assert.Same(retainedSource, dock.CollapsedNotes);
                Assert.Null(((Border)dock.FindName("CollapsedView")).ToolTip);
                Assert.Equal(text.Text, retainedTabs.First(t => t.Note!.Id == note.Id).Note!.Text);
                foreach (var row in Descendants<Grid>(library).Where(g => g.DataContext is NoteRow && g.Children.OfType<System.Windows.Shapes.Ellipse>().Any()))
                {
                    var label = row.Children.OfType<TextBlock>().First();
                    Assert.DoesNotContain("\r", label.Text);
                    Assert.True(label.ActualHeight < label.FontSize * 2);
                    AssertCentered(row.Children.OfType<System.Windows.Shapes.Ellipse>().Single(), label, row);
                }
                AssertCentered((FrameworkElement)editor.FindName("PinDot"), (FrameworkElement)editor.FindName("PinText"), editor);
                for (var i = 0; i < 4; i++)
                {
                    ((Button)editor.FindName("PinButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    dock.UpdateLayout();
                    Assert.Same(retainedSource, dock.CollapsedNotes);
                    Assert.Equal(retainedTabs, Descendants<DockTab>(dock));
                    Assert.Equal(0, unloads);
                }
                var searchControl = Descendants<Ledge.App.Controls.SearchBox>(library).Single();
                searchControl.SearchText = "query";
                searchControl.FocusSearch();
                var searchInput = Descendants<TextBox>(searchControl).Single();
                library.UpdateLayout();
                Assert.Equal(0, searchInput.CaretIndex);
                Assert.InRange(searchInput.GetRectFromCharacterIndex(0).X, 34, 41);
                searchControl.SearchText = "";
                Assert.Equal(text.ContextMenu!.Items.OfType<MenuItem>().Select(m => m.Header), searchInput.ContextMenu!.Items.OfType<MenuItem>().Select(m => m.Header));
                Assert.All(searchInput.ContextMenu.Items.OfType<MenuItem>(), m => Assert.Same(searchInput, m.CommandTarget));
                searchInput.ContextMenu.IsOpen = true;
                Pump(100);
                Assert.IsType<ContinuousSurface>(searchInput.ContextMenu.Template.FindName("MenuSurface", searchInput.ContextMenu));
                Capture(searchInput.ContextMenu, "search-menu");
                searchInput.ContextMenu.IsOpen = false;
                var noteMenu = ((Grid)noteWindow.FindName("NoteRoot")).ContextMenu;
                Assert.Equal("Delete note", ((MenuItem)noteMenu.Items[^1]).Header);
                Assert.Equal("Destructive", ((MenuItem)noteMenu.Items[^1]).Tag);
                noteWindow.Activate();
                text.Focus();
                text.Select(0, 7);
                text.ContextMenu!.IsOpen = true;
                CommandManager.InvalidateRequerySuggested();
                Pump(100);
                text.ContextMenu.UpdateLayout();
                Assert.IsType<ContinuousSurface>(text.ContextMenu.Template.FindName("MenuSurface", text.ContextMenu));
                Assert.True(text.ContextMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Copy")).IsEnabled);
                Capture(text.ContextMenu, "text-menu");
                text.ContextMenu.IsOpen = false;
                foreach (var color in Enum.GetValues<NoteColor>())
                {
                    ((Button)editor.FindName("ColorButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var popup = (Popup)editor.FindName("ColorPopup");
                    popup.Child.UpdateLayout();
                    if (color == NoteColor.Butter) Capture((FrameworkElement)popup.Child, "color-palette");
                    var swatch = Descendants<Button>(popup.Child).Single(b => b.DataContext is ColorChip chip && chip.Color == color);
                    swatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.False(popup.IsOpen);
                    Assert.Equal(color, store.Notes.Single(n => n.Id == note.Id).Color);
                    Assert.Equal(color, editor.Note!.Color);
                    dock.UpdateLayout();
                    Assert.All(Descendants<DockTab>(dock).Where(t => t.Note?.Id == note.Id), t => Assert.Equal(color, t.Note!.Color));
                    Assert.Equal((Color)ColorConverter.ConvertFromString(color.ToHex()), ((LinearGradientBrush)((Border)editor.FindName("NoteSurface")).Background).GradientStops.Last().Color);
                }
                ((Button)editor.FindName("ColorButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(noteWindow), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                editor.RaiseEvent(escape);
                Assert.True(escape.Handled);
                Assert.False(((Popup)editor.FindName("ColorPopup")).IsOpen);
                Assert.True(noteWindow.IsVisible);
                Capture(noteWindow, "note");
                noteWindow.Close();
                library.UpdateLayout();
                Assert.Contains(Descendants<TextBlock>(library), t => t.Text == "Last edited");
                Capture(library, "library");
                library.Close();
                foreach (var edge in Enum.GetValues<DockEdge>())
                {
                    var previousEdge = settings.DockEdge;
                    settings.DockEdge = edge;
                    if (previousEdge != edge)
                    {
                        var travel = (Window?)typeof(DockWindow).GetField("_travelWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dock);
                        Assert.NotNull(travel);
                        var stage = (Grid)((Canvas)travel!.Content).Children[0];
                        var slide = (TranslateTransform)stage.RenderTransform;
                        var origin = new Point(slide.X, slide.Y);
                        var destination = new Point(dock.Left, dock.Top);
                        var frames = new GifBitmapEncoder();
                        var captureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                        captureTimer.Tick += (_, _) => { if (travel.IsVisible) CaptureTravelFrame(travel, frames); };
                        if (Environment.GetEnvironmentVariable("LEDGE_CAPTURE_DIR") != null) captureTimer.Start();
                        Pump(180);
                        Assert.NotEqual(origin, new Point(slide.X, slide.Y));
                        var first = new Point(slide.X, slide.Y);
                        var at180 = Snapshot(travel);
                        dock.UpdatePosition(); // Display/tray callbacks cannot compete with travel.
                        dock.Collapse();
                        Assert.Equal(destination, new Point(dock.Left, dock.Top));
                        Pump(180);
                        Assert.True(travel.IsVisible);
                        Assert.NotEqual(first, new Point(slide.X, slide.Y));
                        var at360 = Snapshot(travel);
                        Pump(180);
                        Assert.True(travel.IsVisible);
                        var at540 = Snapshot(travel);
                        Pump(350);
                        captureTimer.Stop();
                        Assert.False(travel.IsVisible);
                        Assert.Equal(1, dock.Opacity);
                        if (Environment.GetEnvironmentVariable("LEDGE_CAPTURE_DIR") is { } captures)
                        {
                            SaveSnapshot(at180, Path.Combine(captures, "edge-travel-" + edge + "-180ms.png"));
                            SaveSnapshot(at360, Path.Combine(captures, "edge-travel-" + edge + "-360ms.png"));
                            SaveSnapshot(at540, Path.Combine(captures, "edge-travel-" + edge + "-540ms.png"));
                            using var output = File.Create(Path.Combine(captures, "edge-travel-" + edge + ".gif"));
                            frames.Save(output);
                        }
                    }
                    dock.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    typeof(DockWindow).GetMethod("Peek", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dock, null);
                    Pump();
                    var warmZone = (Border)dock.FindName("HoverZone");
                    Assert.True(warmZone.IsVisible && warmZone.ActualWidth > 0);
                    foreach (var tab in Descendants<DockTab>(dock).Where(t => t.PeekOnly))
                    {
                        var label = (TextBlock)tab.FindName("PeekWord");
                        Assert.True(label.IsVisible);
                        var bounds = label.TransformToAncestor(dock).TransformBounds(new Rect(label.RenderSize));
                        Assert.True(bounds.Right > 10 && bounds.Left < dock.ActualWidth - 10, $"Peek text offscreen on {edge}: {bounds}");
                    }
                    var hoverTab = Descendants<DockTab>(dock).First(t => t.PeekOnly);
                    var hoverSlide = (TranslateTransform)hoverTab.FindName("Slide");
                    var resting = new Point(hoverSlide.X, hoverSlide.Y);
                    var hoverEvent = new MouseEventArgs(Mouse.PrimaryDevice, 0);
                    typeof(DockTab).GetMethod("Tab_MouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hoverTab, new object[] { hoverTab, hoverEvent });
                    Assert.Equal(resting, new Point(hoverSlide.X, hoverSlide.Y));
                    Pump(90);
                    Assert.NotEqual(resting, new Point(hoverSlide.X, hoverSlide.Y));
                    Assert.True(Math.Abs(hoverSlide.X) + Math.Abs(hoverSlide.Y) > .1, "Hover enter snapped to its target.");
                    Pump();
                    var expanded = new Point(hoverSlide.X, hoverSlide.Y);
                    typeof(DockTab).GetMethod("Tab_MouseLeave", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hoverTab, new object[] { hoverTab, hoverEvent });
                    Assert.Equal(expanded, new Point(hoverSlide.X, hoverSlide.Y));
                    Pump(90);
                    Assert.NotEqual(expanded, new Point(hoverSlide.X, hoverSlide.Y));
                    Assert.NotEqual(resting, new Point(hoverSlide.X, hoverSlide.Y));
                    Pump();
                    var mouse = new MouseEventArgs(Mouse.PrimaryDevice, 0);
                    typeof(DockWindow).GetMethod("Window_MouseLeave", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dock, new object[] { dock, mouse });
                    typeof(DockWindow).GetMethod("Window_MouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dock, new object[] { dock, mouse });
                    Pump();
                    Assert.True(dock.IsPeeking);
                    Capture(dock, "dock-" + edge);
                }
                settings.DockEdge = DockEdge.Right;
                dock.Expand();
                Pump();
                foreach (var tab in Descendants<DockTab>(dock).Where(t => !t.PeekOnly && t.Note!.Pinned))
                    AssertCentered((FrameworkElement)tab.FindName("PinnedDot"), (FrameworkElement)tab.FindName("HeaderWord"), tab);
                Capture(dock, "dock-expanded");
                dock.Close();
                var manager = new WindowManager();
                manager.Initialize(store, settings);
                using var tray = new SystemTrayService();
                tray.Initialize(manager);
                for (var i = 0; i < 2; i++)
                {
                    if (i == 1) app.Resources.MergedDictionaries.Add(dark);
                    typeof(SystemTrayService).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tray, null);
                    var menu = (ContextMenu)typeof(SystemTrayService).GetField("_contextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
                    Assert.Equal(PlacementMode.MousePoint, menu.Placement);
                    menu.UpdateLayout();
                    Assert.IsType<ContinuousSurface>(menu.Template.FindName("MenuSurface", menu));
                    Assert.Equal(2, menu.Items.OfType<Separator>().Count());
                    foreach (var separator in menu.Items.OfType<Separator>())
                    {
                        var rule = (Border)separator.Template.FindName("Rule", separator);
                        Assert.NotNull(rule);
                        Assert.True(rule.ActualWidth >= menu.ActualWidth - 40, $"Divider too short: {rule.ActualWidth} / {menu.ActualWidth}");
                    }
                    foreach (var separator in menu.Items.OfType<Separator>())
                    {
                        var rule = (Border)separator.Template.FindName("Rule", separator);
                        Assert.Equal(((SolidColorBrush)app.FindResource("MenuDividerBrush")).Color, ((SolidColorBrush)rule.Background).Color);
                    }
                    Capture(menu, i == 0 ? "tray-menu" : "tray-menu-dark");
                    menu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Settings"))
                        .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var unified = Assert.Single(app.Windows.OfType<LibraryWindow>());
                    Assert.True(((ContentControl)unified.FindName("SettingsHost")).IsVisible);
                    var icon = (TaskbarIcon)typeof(SystemTrayService).GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
                    icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayMouseDoubleClickEvent));
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Same(unified, Assert.Single(app.Windows.OfType<LibraryWindow>()));
                    Assert.False(((ContentControl)unified.FindName("SettingsHost")).IsVisible);
                    unified.Close();
                }
                app.Resources.MergedDictionaries.Remove(dark);
                // This used to deadlock with a DispatcherSynchronizationContext.
                store.ForceSave();
                settings.SaveAsync().GetAwaiter().GetResult();
                Assert.Contains("Updated note", File.ReadAllText(persistence.GetStorePath()));
                app.Shutdown();
            }
            catch (Exception exception) { failure = exception; Console.WriteLine(exception); }
            finally
            {
                Application.Current?.Shutdown();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "UI construction or shutdown hung.");
        Assert.Null(failure);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T item) yield return item;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void AssertCentered(FrameworkElement dot, FrameworkElement label, Visual ancestor)
    {
        var dotCenter = dot.TransformToAncestor(ancestor).Transform(new Point(0, dot.ActualHeight / 2)).Y;
        var labelCenter = label.TransformToAncestor(ancestor).Transform(new Point(0, label.ActualHeight / 2)).Y;
        Assert.InRange(Math.Abs(dotCenter - labelCenter), 0, 1);
    }

    private static void Pump(int milliseconds = 600)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void CaptureTravelFrame(Window travel, GifBitmapEncoder frames)
    {
        // Exercise real WPF rendering during travel, not just the target transform value.
        var width = 800;
        var height = (int)(width * travel.ActualHeight / travel.ActualWidth);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.DimGray, null, new Rect(0, 0, width, height));
            drawing.DrawRectangle(new VisualBrush((Visual)travel.Content), null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var metadata = new BitmapMetadata("gif");
        metadata.SetQuery("/grctlext/Delay", (ushort)3);
        frames.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
    }

    [Fact]
    public void SpringSettlesAndRetargetsWithoutResettingVelocity()
    {
        double value = 0, velocity = 0;
        var maximum = 0.0;
        for (var i = 0; i < 120; i++)
        {
            SpringMotion.Advance(ref value, ref velocity, 100, 1.0 / 60);
            maximum = Math.Max(maximum, value);
        }
        Assert.InRange(maximum, 100.01, 105);
        Assert.InRange(value, 99.999, 100.001);
        SpringMotion.Advance(ref value, ref velocity, 0, .05);
        Assert.True(velocity < 0 && value > 0);
        for (var i = 0; i < 60; i++) SpringMotion.Advance(ref value, ref velocity, 50, 1.0 / 30);
        Assert.InRange(value, 49.999, 50.001);
    }

    private static void Capture(FrameworkElement window, string name, bool settle = true)
    {
        if (settle) Pump();
        window.UpdateLayout();
        var directory = Environment.GetEnvironmentVariable("LEDGE_CAPTURE_DIR");
        if (directory == null) return;
        Directory.CreateDirectory(directory);
        SaveSnapshot(Snapshot(window), Path.Combine(directory, name + ".png"));
    }

    private static RenderTargetBitmap Snapshot(FrameworkElement window)
    {
        var bitmap = new RenderTargetBitmap((int)(window.ActualWidth + window.Margin.Left + window.Margin.Right), (int)(window.ActualHeight + window.Margin.Top + window.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        return bitmap;
    }

    private static void SaveSnapshot(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
