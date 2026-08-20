---
version: 1
slug: "src-screens-livecontrol"
primary_target: "src/screens/LiveControl"
related_targets: []
---

## Scope and mode

The operator's live control screen — the one surface used *during* a service. Visitor mode: **Operate**. The separate audience output window is referenced by this brief but is not built in this pass.

## Audience and job

A church service operator in a dim media booth at the back of the sanctuary, often a rotating volunteer. They follow the pastor or worship leader in real time and put the right thing on the congregation's screen. Success is speed with a low error rate, and above all never accidentally putting the wrong thing on air.

## Task and content

Push four confirmed content types to a separate audience display: Bible verses, song lyrics, announcements and sermon slides, and media (video, countdown, image).

Navigation is confirmed as **both**: a prepared run-of-service is the spine, and the operator can break away at any moment to search and push anything on the fly. Free-form pushes must never disturb the prepared spine.

Confirmed architecture: **dual-screen** — an operator control view driving a separate audience-facing output.

## Chosen direction: The Lighting Desk

Visual authority is a theatre and broadcast lighting control desk. Brushed panel graphite, backlit membrane keys, amber segmented readouts, engraved legends.

- **Structural thesis.** Preview and program are two banked windows, ringed green and red — the only saturated colour on the desk. A numbered cue stack runs down the left. Blind edits happen in preview and cannot reach the congregation until GO.
- **Memorable moment / signature interaction.** One oversized GO key on the bottom rail, separated from every other control by a real gap so nothing lands on it by accident. The take — preview crossing into program, both rings changing state together — is the surface's focal event.
- **Implementation consequence.** The bottom control rail is fixed-position and never reflows. Muscle memory is a hard requirement, not a nicety.
- **Ground.** Dark, forced by the physical scene: a cream or paper ground lights the operator's face in a dim booth.

## States and ranges

Service run: 6–40 items typical. A song: 4–12 lyric pages. A verse push: 1–5 verses at a time.

Material states: empty service, live, preview, armed, skipped, deferred, search with no results, media loading, output disconnected, blackout.

## Anti-goals

- No thumbnail-grid-plus-library-tree ProPresenter clone.
- No cream or paper ground.
- No control that changes position between states.
- No borrowing of professional lighting-desk clutter (banks, submasters, blind-mode jargon). Take the desk's safety discipline, not its density.

## Honest risk

The desk is a convention borrowed from a neighbouring trade. A church volunteer is not a lighting operator, and real desk density can read as intimidating gear rather than a Sunday morning tool.

## Open decisions — do not invent

- Product positioning and differentiator. The user was asked directly and answered "not decided yet."
- The full content-type list beyond the four confirmed above.
- React build tooling, routing, and state approach.
