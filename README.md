# Ledge

<p align="center">
  <img src="assets/icon.png" alt="Ledge icon" width="128" height="128"/>
</p>

<p align="center">
  <strong>Sticky notes that live on the edge of your screen.</strong>
</p>

<p align="center">
  <a href="https://github.com/chuakhaiyi/Ledge/releases/latest/download/Ledge-Setup.exe">
    <img src="https://img.shields.io/badge/Download-Latest%20Release-blue?style=for-the-badge" alt="Download"/>
  </a>
  <a href="https://github.com/chuakhaiyi/Ledge/releases">
    <img src="https://img.shields.io/github/v/release/chuakhaiyi/Ledge?style=for-the-badge" alt="Latest Release"/>
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-MIT-green?style=for-the-badge" alt="MIT License"/>
  </a>
  <a href="https://dotnet.microsoft.com/en-us/download/dotnet/8.0">
    <img src="https://img.shields.io/badge/.NET-8.0-purple?style=for-the-badge" alt=".NET 8.0"/>
  </a>
</p>

---

## Features

| Feature | Description |
|---------|-------------|
| **Edge Dock** | Notes live as a compact stack at the screen edge (left or right). 20px collapsed, expands on hover. |
| **Hover to Peek** | Hover near the edge → word preview. Hover a specific tab → full note preview. |
| **Click to Open** | Click any tab → opens as a draggable, resizable floating window. |
| **Keyboard Driven** | `Ctrl+Alt+N` new note, `Ctrl+Alt+A` all notes, `Ctrl+Alt+D` focus dock, `Ctrl+.` cycle color, `Esc` close. |
| **8 Colors** | Butter, Clay, Moss, Sky, Blush, Slate, Sand, Ink — muted, paper-like tones. |
| **Pin & Archive** | Pin important notes (always visible, copper dot). Archive to hide without deleting. |
| **10-Second Undo** | Delete a note? You have 10 seconds to undo via toast notification or `Ctrl+Z`. |
| **Light/Dark Mode** | Follows Windows theme automatically, or force Light/Dark in settings. |
| **Zero Network** | No accounts, no cloud, no telemetry, no update checks. Your notes never leave your PC. |
| **Portable Mode** | Store data next to the `.exe` — carry it on a USB stick. |
| **Fullscreen Aware** | Dock auto-hides during games/fullscreen apps, reappears when you return. |
| **Windows Installer** | Per-user installation, Start Menu shortcut, optional startup, and uninstall. No .NET runtime required. |

---

## Quick Start

1. **Download and run** `Ledge-Setup.exe` from [Releases](https://github.com/chuakhaiyi/Ledge/releases/latest).
2. **Install** for your Windows account, then open Ledge from the Start Menu. Optional startup and desktop shortcuts are offered during setup. The installer is currently unsigned.
3. **Use it**:
   - `Ctrl+Alt+N` → New note
   - `Ctrl+Alt+A` → Open library (all notes)
   - `Ctrl+Alt+D` → Focus dock (keyboard navigate with ↑↓, Enter to open, Esc to close)
   - Hover right screen edge → peek your notes

---

## Screenshots

> *Screenshots coming soon — the app is functional and ready to use!*

### Dock (collapsed)
*The 20px copper accent bar at the screen edge*

### Dock (peeked)
*Hover → tabs slide out with word previews*

### Note Window
*Draggable, resizable, 8 colors, pinned indicator*

### Library Window
*Search, sort, pin, archive, export — all notes in one place*

---

## Settings

Double-click the tray icon to open All Notes. Settings shares this window; individual notes remain separate floating windows. Uninstalling Ledge retains notes and settings in `%APPDATA%\Ledge`.

| Tab | Options |
|-----|---------|
| **General** | Start with Windows, Portable mode, Dock edge (Left/Right/Top) |
| **Appearance** | Theme (System/Light/Dark), Show dock only on hover |
| **Hotkeys** | Rebind all 4 hotkeys (shows "Taken" if conflict) |
| **Advanced** | Open data folder, Reset to defaults |

---

## Data Storage

| Mode | Location |
|------|----------|
| **Default** | `%APPDATA%\Ledge\store.json` + `settings.json` |
| **Portable** | Next to `Ledge.exe` (entire folder is self-contained) |

- **Format**: JSON, human-readable, no database
- **Backup**: `store.json.bak` kept as one-step safety net
- **Atomic writes**: Temp file → rename, never corrupts on crash
- **Portable**: Copy the folder anywhere, runs on any Windows 10/11 PC

---

## Building from Source

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Windows 10/11 (for WPF)

### Build
```powershell
git clone https://github.com/chuakhaiyi/Ledge.git
cd ledge
dotnet build Ledge.sln --configuration Release
```

### Build the installer
Install Inno Setup 6, then run:
```powershell
.\build\installer.ps1
```
Output: `artifacts/installer/Ledge-Setup.exe` and its SHA256 checksum. The installer installs to `%LOCALAPPDATA%\Ledge` without administrator rights.

### Publish only (development / portable use)
```powershell
.\build\publish.ps1 -OutputDir artifacts/app
```
Output: `artifacts/app/Ledge.exe` (~72 MB)

---

## Architecture

```
Ledge/
├── src/
│   ├── Ledge.Core/       # Domain logic, models, persistence
│   ├── Ledge.App/        # WPF UI, windows, controls, services
│   └── Ledge.Native/     # Win32 P/Invoke (hotkeys, window styles)
├── tests/
├── build/
│   ├── publish.ps1       # Portable build script
│   ├── installer.ps1     # Installer build script
│   └── Ledge.iss         # Inno Setup definition
├── assets/
│   └── icon.ico
├── .github/workflows/    # CI/CD
├── Ledge.sln
└── LICENSE
```

**Tech Stack**: WPF on .NET 8, C#, Win32 P/Invoke, JSON persistence

---

## Why Ledge?

| Problem | Ledge's Answer |
|---------|----------------|
| "I need a quick note but don't want to open an app" | `Ctrl+Alt+N` → type → `Esc` |
| "My taskbar is cluttered with Notepad windows" | No taskbar entry, no Alt+Tab entry |
| "I accidentally close notes and lose them" | 10-second undo on every delete |
| "I don't want my notes in the cloud" | 100% local, zero network calls |
| "Dark mode flashes white when I open notes" | Follows system theme instantly |
| "I want to carry my notes on a USB" | Portable mode — copy folder, run anywhere |

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

1. Fork the repo
2. Create a feature branch
3. Make your changes
4. Run `dotnet build` and verify
5. Submit a PR

---

## License

[MIT License](LICENSE) — free for personal and commercial use.

---

## Acknowledgments

- Inspired by [Peeky Notes](https://www.peekynotes.in/) and [Hold My Notes](https://holdmynotes.app/)
- Fonts: [Fraunces](https://fonts.google.com/specimen/Fraunces) (OFL) & [General Sans](https://www.fontshare.com/fonts/general-sans) (Fontshare Free)
- WPF, .NET, and the Windows API

---

<p align="center">
  <strong>Ledge — Notes that sit on the edge, not in your way.</strong>
</p>
