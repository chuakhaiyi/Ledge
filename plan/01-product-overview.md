# Ledge — product overview

## The idea in one line

A stack of sticky notes that lives docked to one edge of the screen,
out of the taskbar and out of Alt+Tab, and opens instantly when you need it.

## Who it's for

Someone who thinks in small, short-lived notes — a grocery list, a phone
number, three lines before a call — and doesn't want to open an app, sign
in, or manage a window to write them down. The competing behavior is a
Notepad window nobody closes, or a phone note nobody looks at again.

## What it is

- A slim **dock** anchored to the left or right screen edge, showing each
  note as a small leaning tab.
- Hovering a tab **peeks** it — a larger preview, no click needed.
- Clicking a tab opens that note as its **own small floating window**,
  separate from the dock, so it can sit anywhere on screen while you work.
- A separate **library window** lists every note — search, sort by color
  or date, archive, restore.
- Global hotkeys create a note or open the library from anywhere in
  Windows, without touching the mouse.
- Everything saves to a single local file. No account, no server, no
  background network activity.

## What it deliberately isn't

- Not a note-taking app with folders, tags, rich text, or attachments —
  that's a different product with a different audience.
- Not a productivity suite. No reminders, no calendar, no linking notes
  together.
- Not cloud-synced. Multi-device sync is explicitly out of scope for v1 —
  it's the single biggest source of complexity (conflict resolution,
  accounts, a server to run) for a tool whose whole appeal is that there's
  nowhere for your notes to go.

## Success looks like

- From pressing the hotkey to a cursor in a blank note: under 150ms.
- Idle memory low enough that "is this actually running?" is a fair
  question to ask.
- Someone can delete the app, still open the underlying data file in a
  text editor, and read every note.

## Name and one-line pitch

**Ledge.** Notes that sit on the edge, not in your way.
