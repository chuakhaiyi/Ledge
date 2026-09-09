# Ledge — architecture

## Stack decision

**WPF on .NET 8**, C#.

Reasoning:
- Native Win32 interop (needed for hotkeys, tool-window styles, multi-monitor
  edge detection) is direct and well documented, unlike wrapping it through
  a web-view layer.
- Idle memory footprint is small compared to any Chromium-based shell —
  this matters directly for the "is it actually running?" goal.
- Multi-window-per-note is WPF's native model (`Window` instances), not a
  workaround.
- Mature enough that answers to weird edge-case bugs already exist.

Runner-up: Tauri (Rust + web view) — smaller binary, but doing the Win32
window-style and multi-monitor work from Rust is a second language to
carry for not much practical gain here, given the UI itself is simple
enough not to need a browser engine's layout system.

Ruled out: Electron — idle memory alone (100MB+) contradicts the product's
core premise.

## The three window types

1. **Dock window** — one per monitor edge in use. Topmost, click-through
   when collapsed, `WS_EX_TOOLWINDOW` (no taskbar entry, no Alt+Tab entry).
   Repositions on `WM_DISPLAYCHANGE`.
2. **Note window** — one per open note. Small, borderless, draggable by
   its own surface. Also `WS_EX_TOOLWINDOW`. Destroyed (not hidden) on
   close — nothing lingers.
3. **Library window** — one instance, standard resizable window with a
   taskbar presence (this is the one window that behaves like a normal
   app window, since it's opened deliberately and used for longer).

## Global hotkeys

- Win32 `RegisterHotKey`, wrapped in a hidden message-only window to
  receive `WM_HOTKEY`.
- Three defaults: new note, open library, open archive. Each rebindable
  in Settings.
- If a combo is already claimed by the OS or another app,
  `RegisterHotKey` fails silently at the Win32 level — surface that in
  Settings as "not available" rather than pretending it worked.

## Fullscreen / do-not-disturb detection

- On a timer no tighter than ~1s (not a tight poll loop), check whether
  the foreground window covers the full monitor bounds with no border —
  the standard heuristic for "something is fullscreen here."
- If so, hide dock windows for that monitor until the foreground window
  changes. This is the one place a poll is justified — there's no Windows
  event for "user entered a game," only for foreground-window-changed,
  which fires too often to be the sole trigger reliably.

## Data flow

- Single source of truth: `NoteStore`, an in-memory collection backed by
  one JSON file.
- Every window (dock, note, library) binds to the same `NoteStore`
  instance via `INotifyPropertyChanged` / `ObservableCollection` — no
  message-passing between windows, no polling for changes.
- Writes are debounced: 500ms after the last edit, then flush to disk.
  A crash mid-typing loses at most the last half-second.
- No file-watcher on the JSON file itself — Ledge is the only writer,
  so there's nothing external to react to.

## Startup

- Optional "start with Windows" via a registry Run key (not a scheduled
  task — simpler, and the standard place users expect to find it if they
  go looking in Task Manager's Startup tab).
- On launch: read the store, restore dock position(s) and any notes that
  were open as their own windows last session (optional toggle).

## What's explicitly not built

- No network stack at all — not even for update checks in v1. Update
  checks are a deliberate v2 decision, not an oversight, because "makes
  zero network calls" is easy to state and easy to break by accident.
