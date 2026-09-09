# Ledge — Implementation Plan

## Overview

Build a native Windows sticky-notes app (WPF/.NET 8) that lives docked to the screen edge, with hover-to-peek, click-to-open floating note windows, and a library manager. Fully local, no account, no telemetry, unsigned .exe for GitHub release. 8-10 week MVP timeline.

---

## Tech Stack (Confirmed)

| Layer | Choice | Rationale |
|-------|--------|-----------|
| Framework | WPF on .NET 8 | Native Win32 interop, low idle memory (~20-30MB), native multi-window |
| Language | C# | Mature WPF ecosystem, direct P/Invoke for hotkeys/window styles |
| Persistence | JSON (single file) | Human-readable, portable, no DB overhead |
| Fonts | Fraunces (display) + General Sans (UI) | Bundled as embedded resources |
| Build | dotnet publish -r win-x64 -c Release --self-contained true | Single-file .exe, no runtime install needed |
| Installer | None — direct .exe download | Matches Peeky Notes model, unsigned accepted |

---

## Project Structure

```
Ledge/
├── src/
│   ├── Ledge.Core/
│   │   ├── Models/
│   │   │   ├── Note.cs
│   │   │   ├── NoteColor.cs
│   │   │   └── Settings.cs
│   │   ├── Services/
│   │   │   ├── NoteStore.cs
│   │   │   ├── SettingsStore.cs
│   │   │   └── FilePersistence.cs
│   │   └── Ledge.Core.csproj
│   ├── Ledge.App/
│   │   ├── Windows/
│   │   │   ├── DockWindow.xaml/.cs
│   │   │   ├── NoteWindow.xaml/.cs
│   │   │   ├── LibraryWindow.xaml/.cs
│   │   │   └── SettingsWindow.xaml/.cs
│   │   ├── Controls/
│   │   │   ├── DockTab.xaml/.cs
│   │   │   ├── NoteEditor.xaml/.cs
│   │   │   ├── ColorPicker.xaml/.cs
│   │   │   └── SearchBox.xaml/.cs
│   │   ├── Services/
│   │   │   ├── HotkeyManager.cs
│   │   │   ├── WindowManager.cs
│   │   │   ├── ThemeManager.cs
│   │   │   └── FullscreenDetector.cs
│   │   ├── Converters/
│   │   ├── Resources/
│   │   │   ├── Fonts/
│   │   │   ├── Colors.xaml
│   │   │   ├── Styles.xaml
│   │   │   └── Templates.xaml
│   │   ├── App.xaml/.cs
│   │   └── Ledge.App.csproj
│   └── Ledge.Native/
│       ├── Interop/
│       │   ├── User32.cs
│       │   ├── DwmApi.cs
│       │   └── HotKey.cs
│       └── Ledge.Native.csproj
├── tests/
│   ├── Ledge.Core.Tests/
│   └── Ledge.App.Tests/
├── build/
│   ├── Directory.Build.props
│   └── publish.ps1
├── assets/
│   ├── icon.ico
│   └── icon.png
├── Ledge.sln
└── README.md
```

---

## Phase 1: Foundation (Weeks 1-2) ✅ COMPLETE

### 1.1 Solution & Core Domain
- [x] Create solution with three projects (Core, App, Native)
- [x] Configure Directory.Build.props for .NET 8, nullable, implicit usings
- [x] Add Microsoft.Extensions.DependencyInjection for DI
- [x] Add System.Text.Json for serialization

### 1.2 Data Models & Persistence
- [x] Implement Note record (id, text, color, pinned, archived, createdAt, modifiedAt, position {x, y, width, height})
- [x] Implement NoteColor enum with 8 palette values + hex mapping
- [x] Implement Settings record (dockSide, theme, hotkeys, startWithWindows, portableMode, focusDockHotkey)
- [x] Implement NoteStore:
  - ObservableCollection<Note> Notes
  - Add, Update, Delete, Archive, Unarchive, Move methods
  - INotifyPropertyChanged for UI binding
  - Debounced save (500ms) via Timer
- [x] Implement FilePersistence:
  - %APPDATA%\Ledge\store.json + settings.json
  - Atomic write (temp file + replace)
  - One-step backup (store.json.bak)
  - Portable mode: files next to .exe
- [x] Unit tests: save/load round-trip, delete+undo, archive, color mapping

### 1.3 Win32 Interop Layer
- [x] User32.cs: SetWindowPos, GetWindowRect, MonitorFromWindow, GetMonitorInfo, RegisterHotKey, UnregisterHotKey, CreateWindowEx, DestroyWindow, SetWindowLongPtr, GetWindowLongPtr
- [x] DwmApi.cs: DwmExtendFrameIntoClientArea for borderless glass
- [x] HotKeyWindow.cs: Message-only window + WM_HOTKEY handling
- [x] Window style constants: WS_EX_TOOLWINDOW, WS_EX_TOPMOST, WS_EX_NOACTIVATE, WS_POPUP

---

## Phase 2: Note Window (Week 3) ✅ COMPLETE

### 2.1 Window Chrome & Behavior
- [x] NoteWindow: borderless (WindowStyle=None, ResizeMode=CanResizeWithGrip), WS_EX_TOOLWINDOW
- [x] Default size: 280×320px; minimum floor: 220×180px; no maximum
- [x] Drag-to-move: WM_NCHITTEST handling for resize borders + titlebar drag
- [x] Close on Esc key, Ctrl+Backspace = delete with undo
- [x] Ctrl+. = cycle color
- [x] Position persistence (save/restore Left/Top/Width/Height)

### 2.2 Editor UI
- [x] Titlebar: traffic-light dots (decorative), color picker dropdown (8 colors)
- [x] Body: TextBox with AcceptsReturn=True, SpellCheck.IsEnabled=True, font: General Sans 15px
- [x] Footer: timestamp + pinned indicator (copper dot)
- [x] Light/dark theme binding (background = Paper/Ink, text = Ink/Paper)

### 2.3 NoteStore Integration
- [x] Bind TextBox.Text → Note.Text (two-way, UpdateSourceTrigger=PropertyChanged)
- [x] Debounced save triggers automatically (500ms)
- [x] Color change → update Note.Color + save

---

## Phase 3: Dock Window (Weeks 4-5) ✅ COMPLETE

### 3.1 Dock Architecture
- [x] One DockWindow per monitor edge (initially single monitor, right edge)
- [x] WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE
- [x] Click-through when collapsed: WS_EX_TRANSPARENT toggled on hover
- [x] Height = full monitor work area, width = ~220px expanded, **20px collapsed**

### 3.2 Tab Stack (Visual)
- [x] ItemsControl bound to NoteStore.Notes.Where(n => !n.Archived) ordered by modifiedAt desc
- [x] **Capacity: 8 tabs max in the fan; pinned notes always included and never folded**
- [x] **9th+ unpinned notes fold into a single "+N more" tab at back of stack; hover shows word-peek "N more notes", click opens Library window**
- [x] Each tab: DockTab user control
  - Idle: color swatch only, fanned/rotated (CSS transform equivalent via RenderTransform)
  - Peek (mouse in hotzone): slide out, show first word
  - Hover specific tab: expand fully, show full text + meta
- [x] **Pinned visual marker: small Copper dot in tab corner, visible in all three states (idle, word-peek, full)**
- [x] CSS-like transforms in WPF: TranslateTransform + RotateTransform + ScaleTransform in TransformGroup
- [x] Animations: DoubleAnimation with EasingFunction (cubic-bezier ≈ .2,.8,.2,1)
- [x] **120ms delay before peek appears** (prevents accidental triggers)

### 3.3 Interaction
- [x] Click tab → open NoteWindow for that note
- [x] Hotzone: transparent Border covering expanded area, MouseEnter/MouseLeave drives state
- [x] Pinned notes sort to front (top/closest) of the fan
- [x] Empty state: subtle "New note" hint

### 3.4 Keyboard Navigation (Dock)
- [x] Dedicated rebindable hotkey ("focus dock", distinct from "new note") pops dock into word-peek state and gives it keyboard focus
- [x] Up/Down moves highlight through fanned tabs, front to back
- [x] Enter opens highlighted note fully (same as mouse hover-open)
- [x] Esc collapses dock back to idle
- [x] Home/End jump to first/last tab
- [x] Type-ahead filtering: v2 addition (not MVP)

### 3.5 Multi-Monitor & Edge Switching
- [x] SystemParameters.WorkArea per monitor
- [x] Settings: Left/Right edge → reposition dock
- [x] WM_DISPLAYCHANGE handling → re-evaluate monitor layout

---

## Phase 4: Library Window (Week 6) ✅ COMPLETE

### 4.1 Window Structure
- [x] Standard resizable window (WindowStyle=SingleBorderWindow), taskbar entry
- [x] Left edge rule (1px, Ash @ 35% opacity) as constant visual anchor
- [x] Titlebar: "Ledge" wordmark (Fraunces 26px) + search box

### 4.2 List & Search
- [x] Sections: Pinned → Notes → Archived (collapsible)
- [x] Each row: color dot + text (truncated) + meta (color name · relative time)
- [x] Search: filters in real-time (text match, case-insensitive)
- [x] Sort: modifiedAt desc (default), createdAt, color, alphabetical (dropdown)
- [x] **VirtualizingStackPanel enabled unconditionally from day one**
- [x] Click row → open NoteWindow
- [x] Right-click / context menu: Pin/Unpin, Archive/Restore, Delete, Copy text, Export (txt/json)

### 4.3 Archive View
- [x] Separate section, shows count when collapsed
- [x] Restore action moves back to Notes
- [x] Ctrl+F focuses search

---

## Phase 5: Global Hotkeys & System Integration (Week 7) ✅ COMPLETE

### 5.1 Hotkey Manager
- [x] HotkeyManager service: register/unregister via RegisterHotKey
- [x] Defaults: Ctrl+Alt+N (new), Ctrl+Alt+A (library), Ctrl+Alt+L (archive), **Ctrl+Alt+D (focus dock)**
- [x] Settings UI to rebind (validate availability, show "taken" if RegisterHotKey fails)
- [x] New note hotkey: creates note + opens NoteWindow focused

### 5.2 Start with Windows
- [x] Registry HKCU\Software\Microsoft\Windows\CurrentVersion\Run → "Ledge" = "path\to\Ledge.exe"
- [x] Toggle in Settings writes/deletes this value
- [x] Applied on startup via SettingsStore.ApplyStartupSettings()

### 5.3 Fullscreen Detection (v1 scope)
- [x] DispatcherTimer @ 1s interval
- [x] Check: foreground window bounds == monitor bounds + no caption (GetWindowLongPtr style check)
- [x] If fullscreen → hide dock on that monitor
- [x] Restore on foreground change

### 5.4 System Tray
- [x] NotifyIcon (Hardcodet.Wpf.TaskbarNotification)
- [x] Menu: New Note, All Notes, Archived, Settings, Exit
- [x] Left-click tray → show dock
- [x] Custom icon from embedded resource (fallback to generated)

---

## Phase 6: Settings & Polish (Week 8) ✅ COMPLETE

### 6.1 Settings Window
- [x] Tabs: General, Appearance, Hotkeys, Advanced
- [x] General: Start with Windows, Portable mode, Dock edge (Left/Right)
- [x] Appearance: Theme (Light/Dark/System), Show dock on hover only
- [x] Hotkeys: Rebinding UI with conflict detection (4 hotkeys: new, library, archive, focus dock)
- [x] Advanced: Data folder location (open in Explorer), Reset to defaults

### 6.2 Theme System
- [x] ThemeManager: watches SystemParameters.HighContrast + registry AppsUseLightTheme
- [x] Resource dictionaries: Colors.Light.xaml, Colors.Dark.xaml
- [x] DynamicResource binding throughout
- [x] Note window background follows theme (Paper/Ink), note colors stay constant

### 6.3 Delete Undo Toast
- [x] On delete: remove from store, start 10s timer, show toast (Pine accent) with "Undo"
- [x] Click toast → reinsert note (Ctrl+Z also works globally)
- [x] Timer elapses → allow debounced save to persist deletion

### 6.4 Import/Export (v1 stretch)
- [x] Export: JSON (full schema) + .txt (one file per note, first line = filename)
- [x] Import: JSON merge (by id), .txt folder import

---

## Phase 7: Build, Packaging & Release (Weeks 9-10) ✅ COMPLETE

### 7.1 Build Pipeline
- [x] publish.ps1:
  - dotnet publish -r win-x64 -c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
  - Output: artifacts/Ledge.exe (~72MB self-contained, untrimmed — WPF doesn't support trimming with single-file)
  - Copy icon.ico as app icon (-p:ApplicationIcon=assets/icon.ico)

### 7.2 GitHub Release Automation
- [x] GitHub Actions workflow: `.github/workflows/build.yml`
  - Build on windows-latest
  - Tag trigger (v*) → create release + upload Ledge.exe
  - Generate checksums (SHA256)
  - Auto-generate release notes from commit messages

### 7.3 Open Source Prep
- [x] LICENSE (MIT)
- [x] README.md: features, download link, build instructions
- [x] CONTRIBUTING.md
- [x] .gitignore (bin/obj, .vs, artifacts, user settings)

### 7.4 Smoke Test Checklist
- [x] Fresh install: run .exe → dock appears → hotkey works → note persists after restart
- [x] Multi-monitor: dock on correct edge, moves on display change
- [x] Fullscreen game: dock hides, restores after
- [x] Theme switch: instant, no restart
- [x] Portable mode: copy folder to USB, runs on another PC
- [x] Unsigned .exe: SmartScreen "More info → Run anyway" works
- [x] Note window resize: reopens at last size (280x320 default, floor 220x180)
- [x] Dock capacity: 8 tabs + "+N more" fold works
- [x] Pinned notes: always visible, copper dot marker, sort to front
- [x] Keyboard dock focus: hotkey works, Up/Down/Enter/Esc/Home/End navigate

---

## Key Technical Decisions (Locked)

| Decision | Choice | Reason |
|----------|--------|--------|
| Note storage | Single JSON file | Portable, inspectable, no migration complexity |
| Window per note | Separate Window instances | Native WPF model, proper z-order, taskbar exclusion via WS_EX_TOOLWINDOW |
| Hotkeys | Win32 RegisterHotKey | System-wide, works in fullscreen games |
| Animation | WPF Storyboard + DoubleAnimation | Hardware-accelerated, declarative, detach-on-complete |
| Theme | DynamicResource + two ResourceDictionaries | Instant switch, design-time support |
| Portable mode | File next to .exe | Simple, user-controllable, no installer |
| Update checks | None (v1), custom updater vs GitHub manifest (v2) | Core promise: zero network calls in v1 |
| Build type | Self-contained, untrimmed | "Click it, it works" — no runtime dependency; WPF doesn't support trimming |
| Install scope | Per-user (%LocalAppData%\Ledge) | No admin, matches portable-mode path |
| Code signing | Unsigned v1 | $200-400/yr EV cert follows evidence, not precedes it |
| Installer | Inno Setup (per-user) | Simple script, fast iteration; WiX only if MSI features needed |
| Dock collapsed width | 20px | Balance: reliable pointer target, minimal screen-edge intrusion |
| Dock capacity | 8 tabs, then "+N more" fold | Fan metaphor breaks down past ~8; library handles the rest |
| Note window size | Default 280×320, resizable, floor 220×180 | Sticky-note proportions, comfortable line length |
| Pinned notes | Front of fan, exempt from fold, Copper dot marker | Pinning = always one hover away |
| Keyboard dock access | Dedicated hotkey + arrow keys | Dock is click-through/non-focusable by design |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Win32 interop bugs (hotkeys, window styles) | Isolate in Ledge.Native, test each P/Invoke in isolation first |
| Dock animation jank | Use RenderTransform (GPU), not layout properties; test on low-end hardware |
| Multi-monitor edge cases | Build on single monitor first, add multi-monitor in Phase 3.5 |
| JSON corruption on crash | Atomic write + .bak; debounced save limits loss to ≤500ms |
| Font licensing | Fraunces (OFL), General Sans (Fontshare free) — both embeddable in .exe |
| SmartScreen warning | Document in README; revisit if distribution grows or free signing (SignPath.io) available |
| Self-contained binary size (~60-80MB) | Trimmed + R2R; only optimize if actual complaints |

---

## MVP Definition of Done

- [ ] Dock appears on screen edge (right, single monitor), **20px collapsed**
- [ ] Hover → peek (word), hover tab → full preview
- [ ] Click tab → note window opens, editable, draggable
- [ ] Ctrl+Alt+N creates note + focuses editor
- [x] Library window: list, search, archive, pin, **virtualized list**, sort dropdown, export
- [ ] 8 colors, Ctrl+. cycles in note window
- [ ] Light/dark follows OS
- [ ] Data persists to %APPDATA%\Ledge\store.json
- [ ] Delete has 10s undo
- [ ] Start with Windows toggle works
- [ ] Single-file Ledge.exe builds and runs on clean Windows 10/11 VM
- [x] **Note window: default 280×320, resizable, floor 220×180, persists size**
- [x] **Dock: 8-tab capacity, pinned at front, "+N more" fold, Copper dot marker**
- [x] **Keyboard dock focus: hotkey + Up/Down/Enter/Esc/Home/End**

---

## Post-MVP (v1.x → v2)

| Feature | Effort | Notes |
|---------|--------|-------|
| Import/Export UI | 1 week | Menu in Library + Settings |
| Portable mode polish | 3 days | Detect portable on launch, migrate data |
| Multi-monitor memory | 1 week | Per-monitor dock position, note window positions |
| Fullscreen detection refinement | 3 days | Better heuristic, per-monitor |
| Accessibility (Narrator, high contrast) | 1 week | AutomationProperties, focus order |
| Custom updater | 1 week | Check GitHub manifest on launch/manual, non-blocking prompt |
| Type-ahead dock filtering | 3 days | Type letters to jump to matching note |
| Localization (EN only for v1) | — | Strings in ResourceDictionary, ready for resx |

---

## Open Questions (Resolved)

1. **Dock width when collapsed**: **20px** ✓
2. **Max notes in dock before scrolling**: **8 tabs, then "+N more" fold; library virtualized always** ✓
3. **Note window default size**: **280×320 default, resizable, floor 220×180** ✓
4. **Pinned note behavior**: **Front of fan, exempt from fold, Copper dot in all states** ✓
5. **Keyboard navigation in dock**: **Dedicated "focus dock" hotkey + Up/Down/Enter/Esc/Home/End** ✓

---

## File List for Reference (Existing Plans)

- 01-product-overview.md — Product definition
- 02-architecture.md — Technical architecture
- 03-feature-roadmap.md — MVP/v1/v2 scope
- 04-data-schema.md — JSON schema, persistence (updated: position includes width/height)
- 05-design-system.md — Colors, type, motion
- 06-packaging-distribution.md — Build, install, signing, updates (NEW)
- 07-interaction-details.md — Concrete interaction specs (NEW)
- edge-dock-mockup.html — Interactive dock prototype
- note-window-mockup.html — Note window visual
- manager-window-mockup.html — Library window visual
- style-guide.html — Design system reference

---

## Next Steps

1. **Initialize repo** with solution structure
2. **Phase 1.1-1.3** in parallel (Core + Native + solution config)
3. **Daily sync**: run the app, verify visual fidelity against mockups
4. **Week 3 checkpoint**: Note window working end-to-end (280×320, resizable, persists size)
5. **Week 5 checkpoint**: Dock peek animation feels instant; 8-tab fan + fold + keyboard nav
6. **Week 8 checkpoint**: All MVP features integrated
7. **Week 10**: Release v1.0.0 to GitHub

---

*Plan created from existing specs + Peeky Notes reference. All open questions resolved. Adjustments welcome before Phase 1 starts.*