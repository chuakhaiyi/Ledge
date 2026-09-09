# Ledge — data schema

## File layout

```
%APPDATA%\Ledge\
  store.json          the notes
  settings.json        hotkeys, dock side, theme, start-with-windows
  store.json.bak        previous save, kept as a one-step safety net
```

Portable mode (toggle in Settings) writes both files next to the .exe
instead of AppData, so the whole app folder can be copied or moved as a
unit.

## `store.json` shape

```json
{
  "version": 1,
  "notes": [
    {
      "id": "8f3c1e2a-...",
      "text": "Call the vet before 5",
      "color": "moss",
      "pinned": false,
      "archived": false,
      "createdAt": "2026-09-01T09:14:00Z",
      "modifiedAt": "2026-09-01T09:15:22Z",
      "position": { "x": 1200, "y": 340, "width": 280, "height": 320 }
    }
  ]
}
```

Notes:
- `version` allows a migration step later without guessing the shape of
  old files.
- `color` is a name from a fixed palette (see design system), not a hex
  value — keeps re-theming possible without touching saved data.
- `position` is only meaningful if the note was open as a floating
  window when the app closed; otherwise omitted. `width`/`height` capture
  a resized note window (default 280×320, floor of 220×180 — see
  `07-interaction-details.md`) so it reopens at the size it was left,
  not just the place.
- Deleting a note removes it from this file outright — see the undo
  window below for how that's made safe without a separate trash store.

## Delete + undo

- Delete removes the note from the in-memory collection immediately and
  starts a 10-second timer holding the removed note in memory (not yet
  written to disk).
- If undo is pressed inside that window, the note is reinserted.
- If the timer elapses, the debounced save fires as normal with the note
  gone. This avoids a permanent "trash" concept while still making
  delete forgiving.

## Archive vs delete

- Archive sets `archived: true` and excludes the note from the dock and
  default library view, but it stays in `store.json` indefinitely.
- Only explicit delete removes a note from the file.

## Import / export

- Export: write the full `notes` array back out as pretty-printed JSON,
  or as plain `.txt` (one file per note, filename from the first line)
  for someone who just wants their words out, not the format.
- Import: accept the same JSON shape; unrecognized fields are ignored
  rather than rejected, so a hand-edited file doesn't hard-fail.

## What's not versioned or synced

There is deliberately no per-note edit history and no merge logic — this
is a single-writer, single-machine file. Multi-device sync would need a
real conflict-resolution model and is out of scope (see product overview).
