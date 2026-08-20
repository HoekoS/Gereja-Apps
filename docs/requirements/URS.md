# User Requirements Specification

**System:** Church Service Projection Application
**Version:** 0.1 (draft)
**Date:** 2026-08-20
**Status:** Draft — awaiting review by the church

---

## 1. Purpose

This document states what the people of the church need the projection system to do, in their own terms. It describes needs and outcomes, not solutions. How those needs are met is specified in the [Software Requirements Specification](SRS.md) and the [backend design](../superpowers/specs/2026-08-20-projection-backend-design.md).

## 2. Scope

The system puts content on the congregation's screen during a live service, and lets that content be prepared beforehand. It covers Bible passages, song lyrics, announcement and sermon slides, and media.

The system is an internal tool for one congregation. It is not distributed to other churches.

## 3. Definitions

| Term | Meaning |
|---|---|
| **Congregation screen** | The large display the congregation sees. Also called the audience display or output. |
| **Control screen** | The display the operator works on, in the media booth. Never seen by the congregation. |
| **Live** | Content currently on the congregation screen. |
| **Staged** | Content the operator has selected and is reviewing, not yet visible to the congregation. |
| **Service** | An ordered list of items prepared for one gathering. |
| **Item** | One element of a service: a passage, a song, a slide, a media file, or a countdown. |
| **Page** | One screenful of an item. A song has several pages; a passage may have one or several. |
| **Booth machine** | The computer in the media booth that runs the system. |
| **(P)** | Marks a requirement **proposed by the analyst**, not stated by the church. Each needs confirmation before it is built. |

## 4. Stakeholders and roles

Roles, not people. In a congregation this size one volunteer commonly holds two or three of them, sometimes all three in the same morning.

| Role | Description |
|---|---|
| **Operator** | Runs the congregation screen during the live service. Often a rotating volunteer with little training and no rehearsal. |
| **Preparer** | Builds the service order ahead of time — typically Saturday, sometimes minutes before the service starts. |
| **Administrator** | Sets up the booth machine, imports Bible translations and songs, adds media, and knows the PIN. |
| **Congregation** | Does not use the system, but is its entire audience. Every requirement about the congregation screen exists for them. |
| **Pastor / worship leader** | Does not use the system. The operator follows them in real time, which is why the operator's speed and error rate matter. |

## 5. Operating environment

- A dim media booth at the back of the sanctuary. Screen brightness from the control display must not light the operator's face or distract the congregation.
- The operator works under time pressure, following a live human, with no opportunity to undo a mistake the congregation has already seen.
- Church internet is unreliable and must never be in the path of a live service.
- The service runs on a schedule. There is no maintenance window and no second attempt.

## 6. User requirements

### 6.1 Service preparation

| ID | Requirement |
|---|---|
| URS-PREP-01 | A preparer shall be able to create a service identified by a name and a date. |
| URS-PREP-02 | A preparer shall be able to add Bible passages, songs, slides, media, and countdowns to a service as an ordered list. |
| URS-PREP-03 | A preparer shall be able to reorder the items in a service. |
| URS-PREP-04 | A preparer shall be able to remove an item from a service without removing the underlying content from the library. |
| URS-PREP-05 | A preparer shall be able to label any item with text that appears in the run, so the operator can recognise it at a glance. |
| URS-PREP-06 | A preparer shall be able to prepare a service on any day before the service, and that service shall be available unchanged on the service day. |
| URS-PREP-07 | A preparer shall be able to edit a service after the service has begun. |
| URS-PREP-08 **(P)** | A preparer shall be able to copy a past service as the starting point for a new one. |

### 6.2 Live operation

| ID | Requirement |
|---|---|
| URS-LIVE-01 | The operator shall be able to see, at every moment, exactly what the congregation is currently seeing. |
| URS-LIVE-02 | The operator shall be able to stage content and review it before the congregation sees it. |
| URS-LIVE-03 | Staged content shall reach the congregation only through a single deliberate action by the operator. |
| URS-LIVE-04 | The operator shall be able to move to the next page and the previous page of the live item. |
| URS-LIVE-05 | The operator shall be able to work through a prepared service in order without searching for anything. |
| URS-LIVE-06 | The operator shall be able to find and show any Bible passage or song that is not in the prepared service, at any moment during the service. |
| URS-LIVE-07 | Showing an unprepared item shall not alter the prepared service order. |
| URS-LIVE-08 | The operator shall be able to skip a prepared item and return to it later. |
| URS-LIVE-09 | The operator shall be able to blank the congregation screen in one action and restore it in one action. |
| URS-LIVE-10 | The operator shall be able to clear the congregation screen so that nothing is shown. |
| URS-LIVE-11 | The operator shall be able to see whether the congregation screen is connected and receiving content. |
| URS-LIVE-12 | The operator shall be able to hand over to another operator mid-service without interrupting what the congregation sees. **(P)** |

### 6.3 Bible content

| ID | Requirement |
|---|---|
| URS-BIB-01 | The operator shall be able to show verses from Terjemahan Baru, Terjemahan Lama, and at least one English translation. |
| URS-BIB-02 | All Bible text shall be usable with no internet connection. |
| URS-BIB-03 | The operator shall be able to find a passage by its reference — book, chapter, and verse. |
| URS-BIB-04 | The operator shall be able to find a passage by searching for words that appear in the text. |
| URS-BIB-05 | The operator shall be able to change translation while remaining on the same passage. |
| URS-BIB-06 | The operator shall be able to show a range of consecutive verses, broken across pages when the range is too long for one screen. |
| URS-BIB-07 | An administrator shall be able to add a Bible translation to the system. |
| URS-BIB-08 | Book names shall be shown in the language of the selected translation. |

### 6.4 Song content

| ID | Requirement |
|---|---|
| URS-SONG-01 | The operator shall be able to show a song's lyrics page by page. |
| URS-SONG-02 | An administrator shall be able to add songs, and update existing songs, by importing lyrics from a file. |
| URS-SONG-03 | Re-importing a song shall update the existing song rather than create a second copy of it. |
| URS-SONG-04 | Song section labels shall be free text in the church's own vocabulary — "Reff", not a fixed English list. |
| URS-SONG-05 | The operator shall be able to find a song by its title or by words in its lyrics. |
| URS-SONG-06 | All songs shall be usable with no internet connection. |
| URS-SONG-07 | The system shall store a song's author and CCLI number where known. |

### 6.5 Slides and media

| ID | Requirement |
|---|---|
| URS-MED-01 | A preparer shall be able to create a slide containing typed text, for announcements and sermon points. |
| URS-MED-02 | An administrator shall be able to add image and video files to the system. |
| URS-MED-03 | The operator shall be able to show an image, and play a video, on the congregation screen. |
| URS-MED-04 | The operator shall be able to show a countdown running to a specified clock time. |
| URS-MED-05 | All media shall play from local storage with no internet connection. |

### 6.6 Congregation screen

| ID | Requirement |
|---|---|
| URS-OUT-01 | The congregation screen shall be a display separate from the operator's control screen. |
| URS-OUT-02 | The congregation screen shall show only what the operator has sent live. It shall never show the operator's controls, staged content, search results, menus, or error messages. |
| URS-OUT-03 | The congregation screen shall fill the whole display, with no browser or operating-system furniture visible. |
| URS-OUT-04 | Text on the congregation screen shall be legible from the back row of the sanctuary. |

### 6.7 Availability and recovery

These requirements exist because a service cannot be paused, retried, or apologised out of.

| ID | Requirement |
|---|---|
| URS-AVL-01 | The system shall function completely with no internet connection at any point before or during a service. |
| URS-AVL-02 | Failure of the operator's control screen shall not change what the congregation is seeing. |
| URS-AVL-03 | After any part of the system restarts, the congregation screen shall return to the content that was live before the failure, without the operator having to find and select it again. |
| URS-AVL-04 | A missing or unplayable media file shall be detected while the item is staged, not after it has gone live. |
| URS-AVL-05 | The congregation screen shall never display an error message, a blank browser page, or partially loaded content. Where nothing can be shown, it shall show black. |
| URS-AVL-06 | A failed import shall leave all existing content unchanged. |

### 6.8 Access control

| ID | Requirement |
|---|---|
| URS-SEC-01 | Only people the church has authorised shall be able to control the congregation screen. |
| URS-SEC-02 | Authorisation shall use a single shared PIN. Per-person accounts shall not be required. |
| URS-SEC-03 | The PIN shall change automatically once each week, on Saturday, so that access lapses without an administrator having to revoke it. |
| URS-SEC-04 | An administrator shall be able to read the current PIN at the booth machine. |
| URS-SEC-05 | An operator working at the booth machine itself shall not be required to enter the PIN. |

### 6.9 Administration

| ID | Requirement |
|---|---|
| URS-ADM-01 | All content and settings shall be stored on the booth machine, and shall be backed up by copying a small, documented set of files and folders. |
| URS-ADM-02 | A failed import shall tell the administrator what was wrong with the file. |
| URS-ADM-03 **(P)** | A volunteer shall be able to start the system by following written steps, with no developer present. |
| URS-ADM-04 **(P)** | An administrator shall be able to delete a song, media file, or past service. |

## 7. Constraints

| ID | Constraint |
|---|---|
| CON-01 | Single congregation, internal use. The system is not distributed to other churches. |
| CON-02 | Must work fully offline. |
| CON-03 | Runs on the booth machine; other devices connect over the church's local network. |
| CON-04 | Terjemahan Baru is copyright Lembaga Alkitab Indonesia. It is loaded onto the booth machine for this church's own worship. It is not redistributed, and it is not packaged with the software. |
| CON-05 | The control screen must suit a dim booth — a bright or pale interface lights the operator's face. |
| CON-06 | Operators are rotating volunteers. The system must be learnable without training. |

## 8. Assumptions

| ID | Assumption |
|---|---|
| ASM-01 | The church's local network is available and stable inside the building, independently of internet access. |
| ASM-02 | The booth machine has two displays, or a display plus a projector output. |
| ASM-03 | The booth machine is powered on and the system started before the service begins. |
| ASM-04 | One person with basic technical confidence performs the initial import of translations, songs, and media. |
| ASM-05 | Bible translation files and song lyric files can be obtained in a machine-readable format. |

## 9. Out of scope

Named explicitly so that absence reads as a decision rather than an oversight.

- Distribution to, or use by, any other church.
- Video streaming, recording, or broadcast output.
- CCLI usage reporting. The CCLI number is stored (URS-SONG-07); no report is produced from it.
- A stage or confidence monitor for the worship team. See open item OPN-04.
- Printed bulletin or slide handout generation.
- A phone or tablet remote control. Deferred, not rejected.
- Automatic download of Bible translations or songs from the internet. Excluded by CON-02.

## 10. Open items

| ID | Item | Status |
|---|---|---|
| OPN-01 | Product positioning and differentiator. | Asked; answered "not decided yet". Nothing in this document depends on it. |
| OPN-02 | Which English translation to import. | Undecided. Affects import content, not system behaviour. |
| OPN-03 | Whether content types beyond the four in section 2 are needed. | Undecided. |
| OPN-04 | Whether the worship team needs a stage or confidence monitor. | Not asked. Currently out of scope; would be a significant addition. |
| OPN-05 | Whether two operators ever work simultaneously. Determines whether URS-LIVE-12 is real. | Not asked. |
| OPN-06 | All requirements marked **(P)**. | Proposed by the analyst; each needs confirmation. |

## 11. Acceptance

These requirements are met when a full service is run end to end on the real booth machine and real displays, with the sanctuary network live and the building's internet connection physically disconnected, using a service prepared the previous day, including at least one unprepared passage found and shown on the fly, and including a deliberate mid-service restart of both the control screen and the server.
