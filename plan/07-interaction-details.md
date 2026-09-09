# Ledge — interaction details

Specific values and behaviors that `02-architecture.md` and
`05-design-system.md` describe at a conceptual level. This doc is where
those become concrete numbers and rules.

## Collapsed dock width: 20px

16px falls below a reliable pointer target size on standard-DPI displays —
risky for the one interaction the whole product's speed depends on. 24px
starts to eat into the sliver of desktop people deliberately leave visible
at a screen edge (common in snapped/multi-window layouts). 20px is the
balance: enough visible color to register at rest, small enough to stay
out of the way.

## Dock capacity: 8 tabs, then fold

The fan/stack visual (see the edge-dock mockup) is a physical metaphor,
and it stops reading as a legible fan somewhere around 8-9 overlapping
cards. Past that:

- The dock shows the most recent/relevant 8 tabs, **pinned notes always
  included and never folded** (see below).
- A 9th "+N more" tab sits at the back of the stack; hovering it shows a
  word-peek of "N more notes" rather than a title, and clicking it opens
  the library window instead of trying to expand further.

The **library window's** list is a different problem — no visual fan
constraint, just a normal list — so it gets `VirtualizingStackPanel`
turned on unconditionally from the start rather than switching it on
past some note-count threshold. There's no real cost to leaving it on
always, so there's nothing to tune here.

## Note window: default 280×320px, resizable, with a floor

- Default size: 280px wide × 320px tall — sticky-note proportions, a
  comfortable line length at 14-15px UI text.
- Resizable from any edge or corner.
- Minimum size: 220×180px. No maximum — a note that's grown long enough
  to want a bigger window should be allowed one.
- **Schema impact:** the `position` field in `store.json` (see
  `04-data-schema.md`) needs `width`/`height` alongside `x`/`y`, so a
  resized note reopens at the size it was left, not just the place.

## Pinned notes: front of the stack, exempt from folding, marked in every state

- Pinned tabs sort to the front (top/closest) of the dock's fan.
- They are **never** among the ones folded into "+N more" — pinning is
  the promise that a note stays one hover away, so the cap in the
  section above only ever applies to unpinned notes.
- Visual marker: a small **Copper** dot in the tab's corner — Copper is
  already the design system's assigned "pinned" accent token, so this
  reuses it rather than introducing a new signal. The dot is visible in
  all three dock states (idle color sliver, word-peek, full open), not
  just the fully opened note.

## Keyboard navigation in the dock

The dock is click-through and non-focusable at rest by design (that's
what makes it stay out of the way), so ordinary Tab-key navigation from
elsewhere in Windows can't reach it — it needs its own entry point:

- A dedicated, rebindable hotkey ("focus dock," distinct from "new
  note") pops the dock into its word-peek state and gives it keyboard
  focus.
- **Up/Down** moves a highlight through the fanned tabs, front to back.
- **Enter** opens the highlighted note fully (same visual result as a
  mouse hover-open).
- **Esc** collapses the dock back to idle.
- **Home/End** jump to the first/last tab — useful once the fold-at-8
  behavior above is in play.

Type-ahead filtering (typing a few letters to jump to a matching note)
is a reasonable v2 addition once the core keyboard path is proven; not
part of the initial scope.
