# Agent Guide for Ledge

## Project shape

Ledge is a Windows-only .NET 8 desktop application built with WPF and Win32
interop. It is a local-first sticky-note app: notes and settings are stored as
human-readable JSON, with no network or cloud layer in the application.

The solution is split into:

- `src/Ledge.Core`: platform-independent domain models and persistence/services.
  This is where note behavior and settings storage belong.
- `src/Ledge.App`: WPF application startup, windows, controls, resources, and
  app-level services.
- `src/Ledge.Native`: Win32 P/Invoke declarations and native window helpers.
- `tests/Ledge.Core.Tests`: xUnit tests for Core behavior.
- `tests/Ledge.App.Tests`: WPF/application test project; it currently contains
  little or no test coverage compared with the Core tests.
- `build/publish.ps1`: PowerShell single-file publish script.
- `.github/workflows/build.yml`: Windows CI build, publish, artifact upload, and
  tag-based release workflow.

`archive-T5irTW/` is present in the working tree but is not part of the
solution structure documented by the project. Treat it as user/worktree data
unless the task explicitly includes it.

## Commands

Run these from the repository root in PowerShell on Windows.

### Restore, build, run, and test

```powershell
dotnet restore Ledge.sln
dotnet build Ledge.sln --configuration Debug
dotnet build Ledge.sln --configuration Release
dotnet test Ledge.sln --configuration Debug
dotnet run --project src/Ledge.App/Ledge.App.csproj
```

WPF requires Windows 10/11 for running the application. The project files
enable nullable reference types, implicit usings, and WPF; `build/Directory.Build.props`
also exists with shared MSBuild settings, but it is stored under `build/` rather
than beside the solution. Confirm how a project imports it before relying on
those settings for a particular command.

### Publish

The preferred local publish path is the checked-in script:

```powershell
.\build\publish.ps1
```

It accepts `-Configuration`, `-Runtime`, and `-OutputDir` parameters. It
publishes self-contained and single-file, renames `Ledge.App.exe` to
`Ledge.exe`, and writes `Ledge.exe.sha256`.

The equivalent explicit command is:

```powershell
dotnet publish src/Ledge.App/Ledge.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o artifacts
```

The CI workflow runs restore, Release build, this win-x64 publish, executable
renaming, SHA256 generation, and artifact upload. A `v*` tag additionally
creates a GitHub release.

## Runtime architecture and data flow

`src/Ledge.App/App.xaml.cs` is the composition root:

1. It registers app services in a `Microsoft.Extensions.DependencyInjection`
   `ServiceCollection`.
2. On WPF startup it creates `FilePersistence`, loads persisted data, then
   constructs `SettingsStore` and `NoteStore`.
3. It initializes `WindowManager`, `ThemeManager`, `HotkeyManager`,
   `FullscreenDetector`, and `SystemTrayService` in that order.
4. Startup settings are applied and the dock is shown.
5. On exit it forces note persistence and synchronously waits for settings
   persistence.

`WindowManager` owns the long-lived UI relationships. It lazily creates and
reuses the dock, library, settings, and per-note windows; note windows are
keyed by note ID so opening an already-open note activates the existing
window instead of creating a duplicate.

The main control flow is:

```text
global hotkey / tray / WPF event
  -> WindowManager
  -> NoteStore or a WPF Window
  -> NoteStore change notification
  -> dock/library bindings and debounced persistence
```

`NoteStore` is the domain-facing observable collection. It exposes derived
active, pinned, unpinned, and archived views ordered by `ModifiedAt`. Mutations
schedule a save after 500 ms. Delete is intentionally two-phase: the note is
removed immediately, `_pendingDelete` remains restorable for 10 seconds, and
`WindowManager` displays the undo toast.

`FilePersistence` stores:

- Default mode: `%APPDATA%\Ledge\store.json` and `settings.json`.
- Portable mode: files next to the executable (`AppContext.BaseDirectory`).

Writes use a temporary file and rename. Saving notes copies the previous
`store.json` to `store.json.bak` first. Loading attempts the backup if the
primary store cannot be read. Keep these paths and atomic-write semantics
stable when changing persistence.

Notes are records with stable string IDs, UTC timestamps, mutable note fields,
and optional `NotePosition`. Use the existing `With*` methods or `NoteStore`
operations so `ModifiedAt` is updated consistently.

## WPF, theme, and native interop conventions

- Windows belong in `src/Ledge.App/Windows`; reusable controls belong in
  `src/Ledge.App/Controls`; app services belong in
  `src/Ledge.App/Services`.
- Keep domain logic out of WPF and use `Ledge.Core` for note/settings behavior.
- `Resources/Colors.Light.xaml` and `Colors.Dark.xaml` are the theme dictionaries.
  `ThemeManager` replaces the active color resources at runtime. Use
  `DynamicResource` for values that must update when the theme changes and
  `StaticResource` for fixed resource references, matching the existing
  guidance.
- Shared control templates and styles live in `Resources/Templates.xaml` and
  `Resources/Styles.xaml`; colors live in the color dictionaries.
- Dock positioning supports `Left`, `Right`, and `Top`. Changes to edge behavior
  need to account for both layout orientation and screen position.
- Dock, note, and tray behavior relies on Win32 details. `Ledge.Native` holds
  constants, structs, and P/Invoke declarations; `HotKeyWindow` handles the
  message-window side of global hotkeys.
- Windows that should not appear in the taskbar/Alt+Tab use
  `WS_EX_TOOLWINDOW`. Native handles and event subscriptions must be cleaned up
  in `Dispose`, `Closed`, or the corresponding teardown path.
- Prefer `RenderTransform` for animation rather than layout-changing
  animations, and use virtualization for long WPF lists.
- `DockWindow` updates UI state on the dispatcher in response to store/settings
  notifications. Preserve dispatcher marshaling when changing background
  persistence or timer callbacks.

## Code and naming conventions

Follow the existing Microsoft-style C# conventions:

- Nullable reference types are enabled; do not suppress nullability casually.
- Use `var` when the type is obvious and expression-bodied members for simple
  properties or methods.
- Use async/await for I/O. Be aware that existing shutdown and synchronous
  convenience methods call `.Wait()`/`GetResult()` deliberately; do not add
  blocking waits to UI-thread code without checking for deadlocks.
- Use PascalCase for public types and members, `_camelCase` for private fields,
  and event names ending in `...Changed`, `...Requested`, `...Deleted`, or
  `...Restored` according to their role.
- Keep model types in `Models`, services in `Services`, and Win32 declarations
  in `Ledge.Native/Interop`.
- Preserve the repository's file-scoped namespace and implicit-usings style.
- Use `StaticResource`/`DynamicResource` according to whether a resource is
  fixed or theme-switchable; do not duplicate palette values in individual
  windows.

## Testing approach

Tests use xUnit and FluentAssertions. The current representative tests are
small unit tests in `tests/Ledge.Core.Tests` and assert model/settings behavior.
When changing Core behavior, add or update a focused Core unit test.

The app test project references WPF and the Core project, but UI behavior is
currently primarily manual: run the application and exercise dock hover/peek,
keyboard navigation, global hotkeys, note editing, delete/undo, library,
archive, theme switching, display changes, and portable mode as relevant.

There is no observed dedicated UI automation command or test harness. Do not
claim UI behavior is covered by `dotnet test` unless a test has actually been
added for it.

## Important gotchas

- This is a Windows/WPF application, not a cross-platform .NET app. Native
  APIs, monitor work areas, global hotkeys, taskbar behavior, and window styles
  are part of the product behavior.
- `build/Directory.Build.props` contains additional MSBuild settings, but it is
  not beside the solution or project directories. Do not assume those settings
  apply unless the command/project explicitly imports that file. The file's
  Release settings enable trimming and ReadyToRun, while the publish script
  explicitly notes that WPF does not support trimming for its publish scenario;
  validate publish behavior with the checked-in script/CI command.
- `Ledge.App` is a `WinExe`; `dotnet run` starts a GUI process and does not
  provide a console for diagnostics.
- The application uses explicit shutdown mode and exits through
  `Application_Exit`; changes to startup or service lifetime must preserve
  final persistence.
- `NoteStore` uses timers for both debounced saves and the 10-second undo
  window. Changes must consider thread/dispatcher boundaries and timer
  disposal.
- `FilePersistence.LoadAsync` intentionally falls back to the backup store on
  read/deserialize failure. Preserve recovery behavior and avoid replacing it
  with a silent data reset.
- The app's local-first design is an explicit project principle. Do not add
  network calls, telemetry, cloud sync, update checks, or account requirements
  without a deliberate project-level decision.
- The worktree may already contain unrelated user changes. Inspect `git
  status` before editing and do not revert or overwrite changes you did not
  make.

## Change checklist

Before handing off a change:

1. Keep the change in the owning layer (`Core`, `App`, or `Native`) and avoid
   introducing WPF dependencies into Core.
2. Add/update focused Core tests for changed domain behavior; manually verify
   UI/native behavior when applicable.
3. Run the narrowest relevant existing command, then `dotnet build
   Ledge.sln --configuration Release` for changes affecting shared code or
   packaging.
4. For release-facing changes, run `.\build\publish.ps1` and inspect the
   generated `artifacts` output.
5. Review persistence paths, theme resource behavior, dispatcher usage, and
   cleanup of native/event resources before concluding.
