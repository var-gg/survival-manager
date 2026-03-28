# Local Runbook

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This runbook explains how to get the project into a locally runnable prototype state.

## Initial Editor Sequence

1. Open Unity project with `6000.4.0f1`
2. Wait for package import and script compilation
3. Run `SM/Seed/Generate Sample Content`
4. Run `SM/Validation/Validate Content Definitions`
5. Open `Assets/_Game/Scenes/Boot.unity`
6. Press Play

## Seed Data Generation

Use the menu:

- `SM/Seed/Generate Sample Content`

This creates the minimum prototype content definition assets required for combat/meta data experiments.

## Validation

Use the menu:

- `SM/Validation/Validate Content Definitions`

This checks duplicate ids, missing references, missing tactic presets, expedition reward references, and trait pool 3+3 baseline shape.

## Tests

### Unity Test Runner
- EditMode: run all EditMode tests
- PlayMode: run smoke test(s)

### Recommended Manual Checks
- generate content
- validate content
- enter PlayMode from Boot scene
- confirm no DB requirement blocks startup
- confirm JSON fallback remains the safe path

## Persistence Modes

### Default
- JSON save fallback

### Optional Local Dev
- Postgres adapter policy exists, but MVP runtime should not depend on it
- set `SM_PERSISTENCE_MODE=postgres` and `SM_POSTGRES_CONNECTION` only for local experiments

## Placeholder vs Implemented

### Implemented
- content authoring definitions
- seed generation
- validator
- pure C# combat/meta core
- JSON save repository

### Partial / Placeholder
- full playable scene loop
- Postgres repository implementation
- visual polish and production UI

## Next-Step Backlog

- PVP
- advanced crafting
- more synergies
- external asset integration

## Open Questions

- should the runbook later split into setup and daily-dev variants?
- what logs should be captured automatically for each local run?
- when should persistence inspection tools be added to editor menus?
- how should build scene ordering be standardized?
