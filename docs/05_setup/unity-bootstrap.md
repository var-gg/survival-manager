# Unity Bootstrap

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document records the actual bootstrap choices used to move the repository into a prototype-ready Unity state.

## Editor Version

The project is currently aligned to the locally installed Unity editor version:

- `6000.4.0f1`

`ProjectSettings/ProjectVersion.txt` should remain pinned to that installed version unless the team explicitly upgrades.

## Package Policy

This bootstrap step keeps only the minimum package surface needed for the current prototype phase.

### Added / Retained Packages

- URP
- TextMeshPro
- UGUI
- Input System
- Test Framework

### Deferred Packages

- Addressables

#### Why Addressables Was Deferred

Addressables is not required yet to prove the MVP vertical slice.
Adding it now would increase authoring and runtime complexity before the project has validated the playable loop.
If later added, the reason should be documented around content loading, memory management, or build modularity needs.

### Removed / Not Retained as Prototype Requirements

The bootstrap does not keep optional tooling packages as part of the prototype baseline if they are not needed for the current slice.
Examples include collaboration helpers, multiplayer-specific tooling, timeline, visual scripting, and AI navigation packages.

## asmdef Structure

The bootstrap target assembly structure is:

- `SM.Core`
- `SM.Content`
- `SM.Combat`
- `SM.Meta`
- `SM.Persistence.Abstractions`
- `SM.Persistence.Json`
- `SM.Persistence.Postgres`
- `SM.Unity`
- `SM.Editor`
- `SM.Tests`

## Scene Skeleton

The prototype scene skeleton is:

- `Boot`
- `Town`
- `Expedition`
- `Battle`
- `Reward`

These scenes are structural placeholders for the MVP phase and should not be treated as content-complete scenes.

## Folder Scope

Bootstrap work is currently limited to project-owned prototype areas, especially:

- `Assets/_Game/`
- `Assets/Tests/EditMode`
- `Assets/Tests/PlayMode`
- `Assets/ThirdParty`

## Open Questions

- when does Addressables become justified enough to add?
- should the prototype keep all five scenes as separate scene files from day one, or collapse some flows temporarily?
- what is the minimum editor tooling needed to keep asmdef and content growth healthy?
- when should Postgres persistence move from placeholder boundary to real adapter work, if ever?
