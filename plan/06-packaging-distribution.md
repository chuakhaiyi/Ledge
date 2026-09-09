# Ledge — packaging & distribution

Decisions below, with the reasoning, so a future revisit has context instead
of just a checklist.

## Build type: self-contained

Self-contained .NET 8 publish (~60-80MB), trimmed with `PublishTrimmed` and
built `ReadyToRun` to keep startup fast despite the larger payload.

Framework-dependent (~5MB) was the alternative, but it means a first run
can fail with "install .NET 8 Desktop Runtime first" — friction that
directly contradicts the product's own pitch of "click it, it just works."
60-80MB is not a meaningful download cost in 2026.

## Install scope: per-user

Installs to `%LocalAppData%\Ledge`, no admin elevation prompt.

Per-machine (`Program Files`, admin required) makes sense for tools
deployed across shared or managed machines. Ledge is a single-user
utility installed by the person who's going to use it — per-user matches
that, and keeps the install path consistent with the portable-mode plan
in the v2 roadmap (data file next to the exe).

## Code signing: unsigned for v1

No certificate purchase for v1. Unsigned means Windows SmartScreen shows
an "Unknown publisher" warning on first run — expected friction for an
indie tool, not a defect to engineer around prematurely.

Revisit if:
- distribution grows beyond yourself/friends and the warning is visibly
  costing installs, or
- the project qualifies for a free signing path (e.g. SignPath.io's
  program for open-source projects), which removes the cost argument
  entirely.

An EV/OV certificate ($200-400+/year) is a cost that should follow
evidence of real distribution, not precede it.

## Updates (v2): custom updater, not MSIX

A lightweight update check against a manifest file published alongside
GitHub Releases:
1. On launch (or on a manual "Check for updates" button — no silent
   background polling), fetch a small JSON file listing the latest
   version and download URL.
2. If newer than the running version, show a non-blocking prompt.
   Never auto-install without confirmation.

MSIX was the alternative — it buys differential updates and store-managed
trust, but both assume Microsoft Store distribution or a more involved
signing/trust setup than a single-developer project needs. Not worth the
overhead here.

## Installer tool: Inno Setup

Inno Setup, not WiX, as the primary installer — not a fallback.

WiX's strengths (MSI transforms, Group Policy deployment, enterprise
install scenarios) don't apply to a personal utility with one install
path. Inno Setup gets a single-file installer, per-user install support,
and a script simple enough to change in minutes while the app is still
moving weekly during MVP/v1 development. Revisit only if a concrete need
for MSI-specific features shows up later.

## Summary table

| Decision | Choice | Revisit when |
|---|---|---|
| Build type | Self-contained, trimmed + R2R | Binary size becomes an actual complaint |
| Install scope | Per-user | Needed on shared/managed machines |
| Code signing | Unsigned | Real distribution volume, or free signing available |
| Updates | Custom updater vs. GitHub manifest | Considering Store distribution |
| Installer | Inno Setup | A concrete need for MSI-only features appears |
