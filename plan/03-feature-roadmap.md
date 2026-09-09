# Ledge — feature roadmap

Scoped so each stage is a shippable, usable thing on its own — not a
partial app waiting for the next stage to be useful.

## MVP — prove the core loop works

- Create / edit / delete a note (delete has the 10s undo from the data
  schema doc)
- Dock anchored to one edge, hover-to-peek, click-to-open as its own
  window
- One global hotkey: new note
- Local JSON persistence, debounced save
- Single note color (no picker yet)
- Light mode only

**Cut line:** if the peek-and-open loop doesn't feel instant, nothing
else in the plan matters — this stage is entirely about that feel.

## v1 — the everyday-usable version

- Full 8-color palette + `Ctrl+.` cycle shortcut while editing
- Library window: list, search, sort by date/color
- Archive (soft-delete, excluded from dock) separate from hard delete
- Remaining hotkeys: open library, open archive
- Dark mode, following the OS setting
- Left/right edge choice in Settings
- Start-with-Windows toggle
- No taskbar entry / no Alt+Tab entry for dock and note windows
  (library window keeps a normal taskbar presence)

## v2 — polish and portability

- Import / export (JSON and plain `.txt`)
- Portable mode (data file next to the .exe)
- Fullscreen/game detection to auto-hide the dock
- Multi-monitor: remember which edge/monitor each note-adjacent setting
  applies to
- Self-update check against a release manifest (the one place a network
  call is introduced — opt-in, and stated plainly in Settings)

## Explicitly not on this roadmap

- Cloud sync / multi-device — see product overview for why
- Rich text, checklists, attachments — different product
- Account system of any kind

## Suggested build order inside MVP

1. `NoteStore` + JSON persistence, no UI at all — get save/load right
   with unit tests before any window exists.
2. Note window (create, edit, close) bound to the store.
3. Dock window: static list first, hover-peek animation second, edge
   anchoring/multi-monitor last.
4. Global hotkey wiring last — it's the smallest piece but the easiest
   to lose an afternoon to on Win32 message-loop quirks, so don't let it
   block everything else.
