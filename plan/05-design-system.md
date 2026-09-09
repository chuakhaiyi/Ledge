# Ledge — design system

## Concept

Ledge's whole premise is physical: notes propped on the edge of something,
like paper tucked along a windowsill or a shelf lip. The design should feel
like that — tactile, slightly architectural, calm — rather than looking
like a SaaS dashboard that happens to contain notes.

The recurring visual motif is **the edge itself**: a single vertical rule
that stands for the screen edge the app lives on. It shows up as the dock's
own silhouette, and as a quiet structural line in the library and settings
windows. It's used because it's literally what the product is, not as
decoration.

## Color

| Token | Hex | Use |
|---|---|---|
| Ink | `#1B1D22` | App chrome background (dark mode), primary text (light mode) |
| Paper | `#EAE6DC` | App chrome background (light mode), note-window base |
| Copper | `#B5642C` | Primary accent — pinned state, active hotkey hints, focus ring |
| Pine | `#4B5A45` | Secondary accent — success/save confirmation, undo toast |
| Ash | `#8A8D91` | Secondary text, timestamps, disabled states |

Note colors (the 8 colors a user can tag a note with) are a **separate**
palette from the chrome above, so the app's own UI never competes with
the notes themselves: Butter, Clay, Moss, Sky, Blush, Slate, Sand, Ink —
each a muted, slightly greyed-down version of the obvious color, not a
bright marker-pen tone. Full hex values live with the implementation,
not here, so they can be tuned against real screens without touching
brand color.

Both a light and dark chrome mode are first-class — the app should follow
the OS setting, not default to one and treat the other as an afterthought.

## Type

- **Display — Fraunces.** Used only for the Ledge wordmark and the
  library window's own title ("Your notes"). Soft, slightly warm serif
  with real character at large sizes; never used below ~20px, since it
  gets heavy in body text.
- **UI — General Sans.** Everything else: note text, labels, buttons,
  settings. Clean, humanist, good at small sizes without looking clinical.

Two families, clearly distinct roles — display is for the two or three
places the product says its own name out loud, UI is for everything a
person actually reads and edits.

## Layout

Left-aligned throughout, not centered — this is a utility, not a poster.
The edge motif (a single vertical rule, `1px`, `Ash` at low opacity) runs
down the left side of the library and settings windows as a quiet
constant, echoing the screen-edge dock.

```
Dock (collapsed)              Dock (peeked)
┌──┐                          ┌──┐──────────────┐
│▏▏│  ← tabs, edge-anchored   │▏▏│ Call the vet  │
│▏▏│                          │▏▏│ before 5      │
│▏▏│                          └──┘──────────────┘
└──┘
```

```
Library window
│ Ledge            [search________]
│
│ Pinned
│  ▸ Call the vet before 5          moss · today
│
│ Notes
│  ▸ Groceries                      clay · yesterday
│  ▸ Standup talking points         sky · 3 days ago
│
│ Archived (3)
```

The vertical rule on the far left is the one constant across every
screen in the app.

## Motion

One deliberate motion moment: the **peek**. Hovering a docked tab slides
it outward with a slight ease and a 120ms delay before appearing, so
passing the mouse over the dock on the way to something else doesn't
trigger a flash of every note. Collapsing reverses the same curve.

Everything else is a plain, fast cross-fade (under 100ms) or nothing at
all. No entrance animations on the library list, no hover-lift on note
tiles — the product's whole pitch is that it gets out of the way, so its
motion should too.

## Self-critique — what was changed and why

First pass leaned on a warm cream background with a terracotta accent —
close to `#F4F1EA` / `#D97757`, which reads as a generic AI-generated
default rather than a choice made for this brief. Revised: **Paper**
darkened and greyed toward `#EAE6DC` (less pink, more like actual paper
stock) and the accent shifted to **Copper** `#B5642C`, which sits further
into rust/brown territory and is less than the brighter, pinker default.

First pass also used a tracked-out all-caps eyebrow label ("NOTES") above
the library list — cut entirely; the section header ("Pinned", "Notes",
"Archived") already carries that information without the label being
announced twice.

Numbered markers (01 / 02 / 03) were considered for the feature list in
the product overview doc but dropped — the features aren't a sequence,
so numbering them would imply an order that doesn't exist.
