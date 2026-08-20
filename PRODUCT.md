# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

React (user's explicit choice for the editor UI). Build tooling, routing, and state approach not yet decided.

## Users

Church service operators (media/AV team) who run the projection screen during a live service. Their job: bring up Bible verses, song lyrics, and other service content on screen while the service is happening.

## Product Purpose

A live presentation tool for church services: prepare content (Bible passages, song lyrics, and other material) ahead of time, then display it on a screen in real time as the service runs. Success is the operator finding and putting the right thing on screen quickly and accurately, without fumbling mid-service.

## Positioning

Not yet decided. User was asked how this differs from Keynote/PowerPoint/Google Slides/Pitch/Gamma and explicitly said "not decided yet" — do not invent a mechanism or angle here; confirm before design work leans on one.

## Operating Context

Live church service: the operator works in real time, generally following the pastor or worship leader, switching what's on screen as the service progresses. Content types beyond "Bible verses" and "song lyrics" are unconfirmed (user said "and etc" without listing them). Whether the operator's screen is the same screen the congregation sees, or a separate control view driving a separate audience-facing output (the common pattern in this software category), is also unconfirmed — flag before assuming a dual-screen architecture.

## Capabilities and Constraints

Confirmed: displays Bible verses and song lyrics. Full content-type list, single- vs dual-screen output, and any live-control mechanism (search, cue list, hotkeys) are undecided and should be confirmed before being designed around.

## Evidence on Hand

None yet. Project is greenfield — no existing code, brand assets, sample content, or reference screenshots provided.

## Product Principles

- Built for live, real-time use under mild time pressure — not an offline authoring tool first.
- Content is prepared ahead of the service, then triggered during it: the prep flow and the live-run flow are likely different modes, not one screen.
- Speed and low error rate for the operator outrank visual flourish — this is Operate-mode software.
- Web app, React-based; no framework/tooling decisions locked in yet beyond that.
