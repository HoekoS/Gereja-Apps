# Software Requirements Specification

**System:** Church Service Projection Application
**Version:** 0.1 (draft)
**Date:** 2026-08-20
**Status:** Draft — awaiting review

---

## 1. Introduction

### 1.1 Purpose

This document specifies what the software must do to satisfy the [User Requirements Specification](URS.md). It is written for whoever implements and tests the system. Every requirement here is intended to be verifiable.

### 1.2 Scope

The system is a local web application that stores service content and drives a congregation-facing display in real time. It comprises one server process on the booth machine and three browser views: control, output, and a deferred remote.

### 1.3 Definitions and abbreviations

| Term | Meaning |
|---|---|
| **Server** | The Node process on the booth machine. Sole authority for live state. |
| **Control view** | Browser view at `/`. The operator's working surface. |
| **Output view** | Browser view at `/output`. Rendered on the congregation display. |
| **Live state** | The server-held record of what is currently on the congregation screen. |
| **Item** | A row of `service_items`; one element of a prepared service. |
| **Page** | One rendered screenful of an item. |
| **GO** | The operator action that promotes staged content to live. |
| **FTS** | Full-text search. SQLite FTS5. |
| **(P)** | Derived from a URS requirement marked proposed. Confirm before building. |

### 1.4 References

| Document | Location |
|---|---|
| User Requirements Specification | [URS.md](URS.md) |
| Backend design | [../superpowers/specs/2026-08-20-projection-backend-design.md](../superpowers/specs/2026-08-20-projection-backend-design.md) |
| Live control screen surface brief | [../../.impeccable/surfaces/src-screens-livecontrol.md](../../.impeccable/surfaces/src-screens-livecontrol.md) |
| Product record | [../../PRODUCT.md](../../PRODUCT.md) |

Where this document and the backend design disagree, this document governs; the design is one way of meeting it.

---

## 2. Overall description

### 2.1 Product perspective

A self-contained system with no external service dependencies. One server process on the booth machine serves the built client application, a REST API, and a WebSocket endpoint over the church's local network. All state lives in one SQLite database file and one media directory on that machine.

The system replaces no existing component and integrates with no other system.

### 2.2 Product functions

| Function | Summary |
|---|---|
| **Library** | Stores and retrieves Bible translations, songs, and media. Provides reference lookup and full-text search. |
| **Import** | Parses Bible and song files and writes them into the library atomically. |
| **Service** | Stores and edits the ordered run of items for a gathering. |
| **Live** | Holds the authoritative live and staged state, applies operator commands, broadcasts state to all clients. |
| **Output** | Renders live state to the congregation display. |
| **Access** | Gates control of the system behind a rotating shared PIN. |

### 2.3 User characteristics

Operators are rotating volunteers with no training and no rehearsal, working under time pressure in a dim booth. Administrators have basic technical confidence but are not developers. No user is available to diagnose a fault during a service.

### 2.4 Constraints

Carried from URS section 7. Additionally:

| ID | Constraint |
|---|---|
| CON-07 | Node.js 20 or later on the booth machine. |
| CON-08 | The client runs in a current Chromium-based browser. Other engines are not a requirement. |
| CON-09 | No component may make an outbound internet request at any time. |
| CON-10 | No relational mapper. Data access is raw SQL against SQLite. |

### 2.5 Assumptions and dependencies

Carried from URS section 8. The system depends on `better-sqlite3`, Fastify, `@fastify/websocket`, React, and Vite. All are vendored into the installation; none is fetched at runtime.

---

## 3. External interface requirements

### 3.1 User interfaces

| ID | Requirement |
|---|---|
| IF-UI-01 | The control view shall be served at `/` and shall be designed per the live control screen surface brief. |
| IF-UI-02 | The output view shall be served at `/output` and shall render only live content, per FR-OUT-01 to FR-OUT-05. |
| IF-UI-03 | The remote view shall be served at `/remote`. It is deferred; the route is reserved and requires no server change when built. |
| IF-UI-04 | The control view shall be legible and operable on a display in a darkened room, with no large light-emitting areas. |
| IF-UI-05 | Controls the operator uses during a live service shall occupy fixed screen positions that do not move between application states. |

### 3.2 Hardware interfaces

| ID | Requirement |
|---|---|
| IF-HW-01 | The output view shall run fullscreen on a display separate from the control view. The system shall not require programmatic control of which physical display is used; the operator positions and fullscreens the window. |
| IF-HW-02 | The system shall place no requirement on the booth machine beyond a dual-display graphics output, sufficient local storage for the media library, and a network interface on the church LAN. |

### 3.3 Software interfaces

| ID | Requirement |
|---|---|
| IF-SW-01 | The server shall expose a REST API over HTTP for library, service, import, and pairing operations. |
| IF-SW-02 | The server shall expose one WebSocket endpoint carrying all live-state traffic. |
| IF-SW-03 | Media files shall be served over HTTP with byte-range support, so that video seeking works. |
| IF-SW-04 | The import interface shall be a single function of the form `parse(buffer) -> { kind, records }`, one implementation per supported file format. |

### 3.4 Communications interfaces

| ID | Requirement |
|---|---|
| IF-COM-01 | All traffic shall be confined to the church's local network. |
| IF-COM-02 | The WebSocket protocol shall be as specified in FR-LIV-10 and FR-LIV-11. |
| IF-COM-03 | Clients shall declare a role of `control`, `output`, or `remote` on connection. |

---

## 4. Functional requirements

### 4.1 Library — Bible

| ID | Requirement |
|---|---|
| FR-LIB-01 | The system shall store multiple Bible translations, each identified by an abbreviation, a name, and a language. |
| FR-LIB-02 | The system shall store verses keyed on a canonical book identifier, a chapter number, and a verse number, independent of the translation's own book naming. |
| FR-LIB-03 | The canonical book identifier shall be an integer not restricted to the range 1–66, so that a translation containing deuterocanonical books can be stored without schema change. |
| FR-LIB-04 | The system shall store per-translation book names and abbreviations, and shall display book names from the selected translation. |
| FR-LIB-05 | The system shall retrieve a verse or a range of consecutive verses given a translation, book, chapter, and verse range. |
| FR-LIB-06 | The system shall return the same passage in a different translation given only a translation identifier and the passage already selected, without re-entry of the reference. |
| FR-LIB-07 | The system shall provide full-text search across verse text, scoped to one translation or across all translations. |
| FR-LIB-08 | The system shall parse a reference typed in free form — including abbreviated and Indonesian book names — into a translation, book, chapter, and verse range. |

### 4.2 Library — Songs

| ID | Requirement |
|---|---|
| FR-LIB-10 | The system shall store songs with a title, an optional author, an optional CCLI number, and a language. |
| FR-LIB-11 | The system shall store a song as an ordered list of pages, each with an optional free-text section label and its text. |
| FR-LIB-12 | Section labels shall be accepted as free text with no fixed vocabulary and no validation against a list. |
| FR-LIB-13 | The system shall provide full-text search across song titles and song page text. |

### 4.3 Library — Media

| ID | Requirement |
|---|---|
| FR-LIB-20 | The system shall store media files on local disk and their metadata — kind, filename, path, duration, width, height — in the database. |
| FR-LIB-21 | The system shall support image and video media kinds. |
| FR-LIB-22 | The system shall impose no artificial size limit on a media file. |
| FR-LIB-23 | The system shall detect that a media file referenced by the database is absent or unreadable, on request, without failing the surrounding operation. |

### 4.4 Import

| ID | Requirement |
|---|---|
| FR-IMP-01 | The system shall import Bible translations from a Zefania XML file. |
| FR-IMP-02 | The system shall import Bible translations from the system's own JSON pack format. |
| FR-IMP-03 | The system shall import songs from plain text, in which the first line is the title, a blank line begins a new page, and a bracketed line or a line ending in a colon is a section label. |
| FR-IMP-04 | The system shall import songs from OpenLyrics XML. |
| FR-IMP-05 | The system shall parse an entire import file to completion before writing any record to the database. |
| FR-IMP-06 | The system shall write all records from one import file inside a single database transaction. |
| FR-IMP-07 | Where an import file is malformed, the system shall write nothing, shall leave existing content byte-for-byte unchanged, and shall report which record or construct was rejected. |
| FR-IMP-08 | Importing a Bible translation whose abbreviation already exists shall replace that translation's verses, not create a second translation. |
| FR-IMP-09 | Importing a song whose title and author both match an existing song shall replace that song's pages, not create a second song. |
| FR-IMP-10 | Import shall be available only to a client that has satisfied FR-SEC-01. |

### 4.5 Service

| ID | Requirement |
|---|---|
| FR-SVC-01 | The system shall create, rename, and delete services, each with a name and a date. |
| FR-SVC-02 | The system shall store service items as an explicitly ordered list. |
| FR-SVC-03 | An item shall have a kind of `verse`, `song`, `slide`, `media`, or `countdown`. |
| FR-SVC-04 | An item shall carry a kind-specific payload: a verse range, a song identifier, inline slide text, a media identifier, or a countdown target time. |
| FR-SVC-05 | An item shall carry an optional operator-facing label, displayed in the run. |
| FR-SVC-06 | The system shall insert, remove, and reorder items in a service, including while that service is live. |
| FR-SVC-07 | Removing an item from a service shall not delete the underlying library content it references. |
| FR-SVC-08 **(P)** | The system shall duplicate an existing service, including all its items, as a new service. |

### 4.6 Live

| ID | Requirement |
|---|---|
| FR-LIV-01 | The server shall be the sole authority for live state. No client shall hold state that the server does not. |
| FR-LIV-02 | The server shall maintain, as live state: the live item and page, the staged item and page, the blackout flag, and the count of connected output clients. |
| FR-LIV-03 | The system shall stage an item and page without any change to what output clients render. |
| FR-LIV-04 | The system shall promote staged content to live only on receipt of an explicit `go` command. No other command shall change live content from staged content. |
| FR-LIV-05 | The system shall advance to the next page and return to the previous page within the live item. |
| FR-LIV-06 | `advance` on the last page of the live item shall hold on that page. It shall not wrap, and shall not move to the next item. |
| FR-LIV-07 | The system shall set and clear a blackout flag, which suppresses all output rendering while set, without altering the live item or page. |
| FR-LIV-08 | The system shall clear live content to nothing. |
| FR-LIV-09 | The Live unit shall read from the Library and Service units and shall never write to either. No live operation shall modify stored content or the prepared service order. |
| FR-LIV-10 | The server shall accept these commands from clients: `preview {itemId, pageIndex}`, `go`, `advance`, `back`, `blackout {on}`, and `clear`. |
| FR-LIV-11 | On every state change the server shall broadcast complete live state — live, staged, blackout, and output count — to every connected client. It shall not send partial updates or deltas. |
| FR-LIV-12 | On connection the server shall send complete current state to the connecting client before any other message. |
| FR-LIV-13 | The server shall persist live state to durable storage on every change. |
| FR-LIV-14 | On start, the server shall restore live state from durable storage. |
| FR-LIV-15 | The system shall stage content that is not part of any service — found by search — and the resulting live push shall leave the prepared service order unchanged. |
| FR-LIV-16 | The system shall mark a service item as skipped without removing it, and shall allow it to be staged and shown afterwards. |
| FR-LIV-17 | The system shall report, on staging, that a staged item's media file is absent or unreadable, and shall refuse the `go` command for that item, stating why. |
| FR-LIV-18 | The live state machine shall be implemented as a unit with no input or output operations, so that all its transitions are testable in isolation. |

### 4.7 Output rendering

| ID | Requirement |
|---|---|
| FR-OUT-01 | The output view shall render live content only. It shall not render staged content, controls, menus, search, or diagnostics under any condition. |
| FR-OUT-02 | Where live content is absent, blackout is set, or the connection to the server is lost, the output view shall render a black screen. |
| FR-OUT-03 | The output view shall render no error message, stack trace, or loading indicator at any time. |
| FR-OUT-04 | The output view shall render a countdown item as a live-updating count to the item's target clock time, computed on the client. |
| FR-OUT-05 | The output view shall reconnect automatically after connection loss, with backoff, and shall render current live state on reconnection with no operator action. |
| FR-OUT-06 | Verse and lyric text shall be rendered at a size legible from the rear of the sanctuary. The exact size is a deployment setting, not a fixed value. |

### 4.8 Access control

| ID | Requirement |
|---|---|
| FR-SEC-01 | The system shall require a valid pairing before accepting any command, any write operation, or any WebSocket connection in the `control` or `remote` role. |
| FR-SEC-02 | Pairing shall be established by submitting a shared PIN of six digits. |
| FR-SEC-03 | The PIN shall be generated using a cryptographically secure random source. |
| FR-SEC-04 | The system shall rotate the PIN when the current PIN was last rotated before the most recent Saturday 00:00 in the booth machine's local time zone. The check shall occur on request; no scheduled process is required. |
| FR-SEC-05 | On successful pairing the system shall issue a token bound to the PIN's rotation timestamp. The token shall become invalid when the PIN rotates. |
| FR-SEC-06 | The token shall be carried in an HTTP cookie marked `HttpOnly` and `SameSite=Lax`, and shall be integrity-protected against modification by the client. |
| FR-SEC-07 | The WebSocket upgrade shall be authorised using the same token as the REST API. |
| FR-SEC-08 | Requests originating from the loopback address shall be exempt from pairing. |
| FR-SEC-09 | The endpoint that reveals the current PIN shall be reachable only from the loopback address. |
| FR-SEC-10 | The `output` role shall be connectable without pairing, since it only receives content and can issue no command. |
| FR-SEC-11 | The system shall log the current PIN to the server console on rotation, so that it is visible at the booth machine. |

### 4.9 Administration

| ID | Requirement |
|---|---|
| FR-ADM-01 | All persistent state shall reside in exactly two locations: one SQLite database file and one media directory. A backup shall consist of copying those two. |
| FR-ADM-02 | The system shall report import failures with the reason and the offending record. |
| FR-ADM-03 | The system shall create its database schema on first start if it is absent. |
| FR-ADM-04 **(P)** | The system shall delete a song, a media file, or a past service on administrator request. |

---

## 5. Non-functional requirements

Thresholds below are proposed by the analyst and derived from the operating scene. They are stated as numbers so they can be tested; confirm them before treating any as a hard acceptance gate.

### 5.1 Performance

Measured on the booth machine over the church LAN, with the library at the capacities in section 5.6.

| ID | Requirement |
|---|---|
| NFR-PERF-01 | The interval from the operator's GO action to the congregation display showing the new content shall not exceed 200 ms. |
| NFR-PERF-02 | Advancing or returning one page shall change the congregation display within 200 ms. |
| NFR-PERF-03 | A verse reference lookup shall return results within 150 ms. |
| NFR-PERF-04 | A full-text search across all installed translations shall return first results within 800 ms. |
| NFR-PERF-05 | A song search shall return results within 300 ms. |
| NFR-PERF-06 | Video playback shall begin within 1 second of going live. |
| NFR-PERF-07 | The server shall be ready to accept connections within 15 seconds of process start. |
| NFR-PERF-08 | A client shall load and display current state within 5 seconds of opening its URL on the LAN. |

### 5.2 Reliability and availability

| ID | Requirement |
|---|---|
| NFR-REL-01 | No function of the system shall depend on internet connectivity. Verification: the system passes its full acceptance run with the building's internet uplink physically disconnected. |
| NFR-REL-02 | Termination, crash, or reload of the control view shall produce no observable change on the congregation display. |
| NFR-REL-03 | Following a server process restart, the congregation display shall return to the content that was live before the restart, within 30 seconds, with no operator action. |
| NFR-REL-04 | Loss of connection between an output client and the server shall cause the output to render black, and shall not cause it to render stale interactive state or any error. |
| NFR-REL-05 | An output client shall re-establish its connection within 5 seconds of the server becoming reachable again. |
| NFR-REL-06 | An import operation shall be atomic. A failure at any point shall leave the database in its pre-import state. |
| NFR-REL-07 | A media upload that fails, including through exhaustion of disk space, shall leave no partial file on disk and no database row. |
| NFR-REL-08 | No single fault in any client shall be capable of corrupting stored content. |
| NFR-REL-09 | The cache shall be optional. An absent, misconfigured, or unreachable cache shall neither prevent the server from starting nor cause any request to fail; the request shall be served from the database instead. |

### 5.3 Security

The threat model is a person on the church's local network — a visitor, a member's guest, a child with a phone — who could otherwise open the control URL and put arbitrary content on the sanctuary screen during a service. It is not a determined remote attacker; the system is never exposed to the internet.

| ID | Requirement |
|---|---|
| NFR-SEC-01 | The server shall bind only to the local network interface and shall not be reachable from outside the church network. Exposing it to the internet is outside the supported configuration. |
| NFR-SEC-02 | Pairing tokens shall be signed with a server-held secret that is generated on first start and stored with the database, and shall not be forgeable by a client. |
| NFR-SEC-03 | The PIN shall not appear in any response reachable from a non-loopback address. |
| NFR-SEC-04 | Uploaded filenames shall be sanitised, and media shall be served only from within the configured media directory. Path traversal outside that directory shall be rejected. |
| NFR-SEC-05 | Rejected pairing attempts shall be rate-limited to a level that makes exhaustive search of a six-digit PIN impractical within one week. |

**Accepted risk, stated explicitly.** Traffic is plain HTTP over the LAN. The PIN and the token therefore cross the wire in cleartext, and anyone able to capture traffic on the church network can read them. This is accepted because the asset being protected is a projection screen in a room where the same people are already physically present, and because terminating TLS on a LAN host would require certificate management that no volunteer can maintain. If the system is ever exposed beyond the sanctuary LAN, this decision must be revisited before that happens.

### 5.4 Usability

| ID | Requirement |
|---|---|
| NFR-USE-01 | The control view shall use a dark ground throughout. No state shall present a predominantly light screen. |
| NFR-USE-02 | Controls used during a live service shall not change position, size, or order between application states. |
| NFR-USE-03 | The action that sends content to the congregation shall be visually and spatially separated from all other controls, such that no adjacent control can be struck in its place. |
| NFR-USE-04 | Live and staged content shall be distinguishable at a glance, without reading text. |
| NFR-USE-05 | Every state listed in the surface brief — empty service, live, staged, armed, skipped, deferred, empty search, media loading, output disconnected, blackout — shall have a defined visual treatment. |
| NFR-USE-06 | An operator who has not used the system before shall be able to run a prepared service after reading a single page of instructions. Verification: observed trial with a volunteer who has not seen the system. |

### 5.5 Maintainability and testability

| ID | Requirement |
|---|---|
| NFR-MNT-01 | The system shall be structured as the units named in section 2.2, each usable and testable without the others running. |
| NFR-MNT-02 | The live state machine shall be free of input and output operations and shall be fully testable through direct function calls. |
| NFR-MNT-03 | Automated tests shall cover the live state transitions, the import parsers including rejection of malformed input, the PIN rotation boundary, and verse search. |
| NFR-MNT-04 | Tests shall run under the Node.js built-in test runner with no additional test framework. |
| NFR-MNT-05 | Deliberate simplifications shall be marked in the source with a comment naming both the resulting ceiling and the upgrade path. |

### 5.6 Capacity

| ID | Requirement |
|---|---|
| NFR-CAP-01 | The system shall hold at least 6 Bible translations of at least 40,000 verses each, meeting the performance requirements of section 5.1 at that size. |
| NFR-CAP-02 | The system shall hold at least 3,000 songs. |
| NFR-CAP-03 | The system shall hold a media library of at least 20 GB, bounded in practice by local disk. |
| NFR-CAP-04 | The system shall retain at least 200 past services. |
| NFR-CAP-05 | The system shall support at least 8 concurrent connected clients. |
| NFR-CAP-06 | A service shall support at least 100 items. |

### 5.7 Portability

| ID | Requirement |
|---|---|
| NFR-PORT-01 | The server shall run on Node.js 20 or later on the booth machine's operating system. |
| NFR-PORT-02 | The client shall run in a current Chromium-based browser. Support for other browser engines is not required. |
| NFR-PORT-03 | The system shall install and run without an internet connection, all dependencies being present in the installation. |

---

## 6. Data requirements

| ID | Requirement |
|---|---|
| DR-01 | All structured data shall be held in a single SQLite database file. |
| DR-02 | Media binaries shall be held on the file system and referenced from the database by path. |
| DR-03 | Live state shall be persisted as a single row, rewritten on every change. |
| DR-04 | Verses shall be keyed on translation, canonical book identifier, chapter, and verse. |
| DR-05 | Full-text indexes shall exist over verse text and over song titles and page text. |
| DR-06 | Item payloads shall be stored as JSON, opaque to SQL. Queries across payload contents are not a requirement. |
| DR-07 | Settings, including the current PIN and its rotation timestamp, shall be stored as key–value rows. |
| DR-08 | No user personal data shall be stored. There are no user accounts. |

---

## 7. Traceability

Every user requirement maps to at least one software requirement. Software requirements with no user-requirement source are marked *derived* and exist to make another requirement achievable.

| URS | SRS |
|---|---|
| URS-PREP-01 | FR-SVC-01 |
| URS-PREP-02 | FR-SVC-02, FR-SVC-03, FR-SVC-04 |
| URS-PREP-03 | FR-SVC-06 |
| URS-PREP-04 | FR-SVC-06, FR-SVC-07 |
| URS-PREP-05 | FR-SVC-05 |
| URS-PREP-06 | FR-SVC-01, DR-01 |
| URS-PREP-07 | FR-SVC-06, FR-LIV-09 |
| URS-PREP-08 (P) | FR-SVC-08 (P) |
| URS-LIVE-01 | FR-LIV-02, FR-LIV-11, NFR-USE-04 |
| URS-LIVE-02 | FR-LIV-03 |
| URS-LIVE-03 | FR-LIV-04, NFR-USE-03 |
| URS-LIVE-04 | FR-LIV-05, FR-LIV-06 |
| URS-LIVE-05 | FR-SVC-02, FR-LIV-03 |
| URS-LIVE-06 | FR-LIB-07, FR-LIB-13, FR-LIV-15 |
| URS-LIVE-07 | FR-LIV-09, FR-LIV-15 |
| URS-LIVE-08 | FR-LIV-16 |
| URS-LIVE-09 | FR-LIV-07, FR-OUT-02 |
| URS-LIVE-10 | FR-LIV-08 |
| URS-LIVE-11 | FR-LIV-02, FR-LIV-11 |
| URS-LIVE-12 (P) | FR-LIV-01, FR-LIV-12 |
| URS-BIB-01 | FR-LIB-01, FR-IMP-01 |
| URS-BIB-02 | CON-09, NFR-REL-01, NFR-PORT-03 |
| URS-BIB-03 | FR-LIB-05, FR-LIB-08 |
| URS-BIB-04 | FR-LIB-07 |
| URS-BIB-05 | FR-LIB-02, FR-LIB-06 |
| URS-BIB-06 | FR-LIB-05, FR-SVC-04 |
| URS-BIB-07 | FR-IMP-01, FR-IMP-02, FR-IMP-08 |
| URS-BIB-08 | FR-LIB-04 |
| URS-SONG-01 | FR-LIB-11, FR-LIV-05 |
| URS-SONG-02 | FR-IMP-03, FR-IMP-04 |
| URS-SONG-03 | FR-IMP-09 |
| URS-SONG-04 | FR-LIB-12 |
| URS-SONG-05 | FR-LIB-13 |
| URS-SONG-06 | CON-09, NFR-REL-01 |
| URS-SONG-07 | FR-LIB-10 |
| URS-MED-01 | FR-SVC-03, FR-SVC-04 |
| URS-MED-02 | FR-LIB-20, FR-LIB-21 |
| URS-MED-03 | FR-LIB-20, IF-SW-03, NFR-PERF-06 |
| URS-MED-04 | FR-SVC-04, FR-OUT-04 |
| URS-MED-05 | CON-09, NFR-REL-01 |
| URS-OUT-01 | IF-UI-02, IF-HW-01 |
| URS-OUT-02 | FR-OUT-01, FR-OUT-03 |
| URS-OUT-03 | IF-HW-01 |
| URS-OUT-04 | FR-OUT-06 |
| URS-AVL-01 | CON-09, NFR-REL-01 |
| URS-AVL-02 | FR-LIV-01, NFR-REL-02 |
| URS-AVL-03 | FR-LIV-13, FR-LIV-14, FR-OUT-05, NFR-REL-03, NFR-REL-05 |
| URS-AVL-04 | FR-LIB-23, FR-LIV-17 |
| URS-AVL-05 | FR-OUT-02, FR-OUT-03, NFR-REL-04 |
| URS-AVL-06 | FR-IMP-05, FR-IMP-06, FR-IMP-07, NFR-REL-06 |
| URS-SEC-01 | FR-SEC-01, NFR-SEC-01 |
| URS-SEC-02 | FR-SEC-02, DR-08 |
| URS-SEC-03 | FR-SEC-04, FR-SEC-05 |
| URS-SEC-04 | FR-SEC-09, FR-SEC-11 |
| URS-SEC-05 | FR-SEC-08 |
| URS-ADM-01 | FR-ADM-01, DR-01, DR-02 |
| URS-ADM-02 | FR-ADM-02, FR-IMP-07 |
| URS-ADM-03 (P) | FR-ADM-03, NFR-PERF-07 |
| URS-ADM-04 (P) | FR-ADM-04 (P) |
| CON-05 | NFR-USE-01, IF-UI-04 |
| CON-06 | NFR-USE-02, NFR-USE-06, IF-UI-05 |
| *derived* | FR-LIB-03, FR-LIB-22, FR-LIV-18, FR-SEC-03, FR-SEC-06, FR-SEC-07, FR-SEC-10, IF-SW-04, NFR-SEC-02, NFR-SEC-04, NFR-SEC-05, NFR-REL-07, NFR-REL-08, NFR-REL-09, NFR-MNT-01 to NFR-MNT-05, NFR-CAP-01 to NFR-CAP-06, NFR-PORT-01 to NFR-PORT-03, DR-03 to DR-07 |

---

## 8. Open items

| ID | Item | Effect if unresolved |
|---|---|---|
| SRS-OPN-01 | React build tooling, routing, and client state approach. | Blocks the frontend implementation plan. Does not affect any requirement in this document. |
| SRS-OPN-02 | Which English translation to import. | Content only. No requirement changes. |
| SRS-OPN-03 | All performance and capacity thresholds in sections 5.1 and 5.6. | Proposed by the analyst. Until confirmed, treat as design targets rather than acceptance gates. |
| SRS-OPN-04 | Rendering of a verse range too long for one page: how the break is chosen and whether the operator can adjust it. | FR-LIB-05 and URS-BIB-06 are satisfiable either way; needs a decision before implementation. |
| SRS-OPN-05 | Whether the output display's font size is a global setting or per item. | Affects FR-OUT-06. |
| SRS-OPN-06 | All requirements marked **(P)**. | Carried from URS OPN-06. |
