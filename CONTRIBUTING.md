# Contributing to Ledge

Thank you for considering a contribution! This document outlines the guidelines for contributing to Ledge.

## Code of Conduct

Be respectful, inclusive, and constructive. This project follows the [Contributor Covenant](https://www.contributor-covenant.org/).

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Windows 10/11 (required for WPF)
- Git

### Setup
```powershell
git clone https://github.com/yourusername/ledge.git
cd ledge
dotnet restore Ledge.sln
dotnet build Ledge.sln --configuration Debug
```

### Run
```powershell
dotnet run --project src/Ledge.App/Ledge.App.csproj
```

## Development Workflow

1. **Create an issue** for bugs or feature requests before starting work
2. **Fork** the repository
3. **Create a branch** with a descriptive name:
   - `fix/issue-123-description`
   - `feat/issue-456-description`
3. **Make changes** following the style guide below
4. **Test** your changes:
   ```powershell
   dotnet build Ledge.sln --configuration Debug
   dotnet test Ledge.sln --configuration Debug
   ```
5. **Submit a PR** with a clear description of what changed and why

## Code Style

### C#
- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `var` when type is obvious
- Prefer expression-bodied members for simple properties/methods
- Use nullable reference types (`#nullable enable`)
- Async/await for I/O operations

### XAML
- Use `StaticResource` for theme colors, `DynamicResource` for theme-switchable resources
- Keep control templates in `Resources/Templates.xaml`
- Styles in `Resources/Styles.xaml`
- Colors in `Resources/Colors.Light.xaml` / `Colors.Dark.xaml`

### WPF Specific
- Prefer `RenderTransform` animations over layout animations (GPU-accelerated)
- Use `VirtualizingStackPanel` for lists
- `WS_EX_TOOLWINDOW` for windows that shouldn't appear in taskbar/Alt+Tab
- Clean up Win32 handles in `Dispose()` / `Closed` events

## Project Structure

```
src/
├── Ledge.Core/           # Domain models, services (no WPF dependency)
│   ├── Models/
│   └── Services/
├── Ledge.App/            # WPF application
│   ├── Windows/          # Main windows
│   ├── Controls/         # Reusable controls
│   ├── Services/         # App-level services
│   └── Resources/        # XAML resources
└── Ledge.Native/         # Win32 P/Invoke (no managed dependencies)
```

## Testing

Currently minimal tests exist. When adding features:
- Unit test Core logic (NoteStore, FilePersistence, SettingsStore)
- UI tests are manual for now — run the app and verify behavior

```powershell
dotnet test Ledge.sln --configuration Debug
```

## Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):
- `fix: ` - Bug fixes
- `feat: ` - New features
- `refactor: ` - Code restructuring
- `docs: ` - Documentation
- `style: ` - Formatting
- `test: ` - Tests
- `chore: ` - Maintenance

Example: `feat: add export to JSON in library context menu`

## Pull Request Checklist

- [ ] Builds without warnings (`dotnet build --configuration Release`)
- [ ] All tests pass (`dotnet test`)
- [ ] Single-file publish works (`dotnet publish ... -p:PublishSingleFile=true`)
- [ ] Changes follow code style
- [ ] No breaking changes without discussion
- [ ] Related issue linked in PR description

## Design Principles

1. **Local-first, zero network** — Never add network calls without discussion
2. **Performance** — Idle memory < 30MB, animations GPU-accelerated
3. **Keyboard accessibility** — Every feature reachable via keyboard
4. **Theme aware** — Light/Dark/System, instant switching
5. **Portable** — Works from USB, no installer required

## Questions?

Open a [Discussion](https://github.com/yourusername/ledge/discussions) or [Issue](https://github.com/yourusername/ledge/issues).

---

Thank you for contributing to Ledge! 📝