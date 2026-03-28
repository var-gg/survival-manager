# MVP Playtest Checklist

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## First Editor Run

1. Open the Unity project with editor `6000.4.0f1`
2. Allow package import/reload to finish
3. Run `SM/Seed/Generate Sample Content`
4. Run `SM/Validation/Validate Content Definitions`
5. Open `Assets/_Game/Scenes/Boot.unity`
6. Confirm project compiles cleanly enough to enter Play Mode
7. Press Play

## Seed Data

Generate sample seed data from:

- `SM/Seed/Generate Sample Content`

Expected generated areas:

- `Assets/_Game/Content/Definitions/Stats`
- `Assets/_Game/Content/Definitions/Races`
- `Assets/_Game/Content/Definitions/Classes`
- `Assets/_Game/Content/Definitions/Traits`
- `Assets/_Game/Content/Definitions/Skills`
- `Assets/_Game/Content/Definitions/Archetypes`
- `Assets/_Game/Content/Definitions/Augments`
- `Assets/_Game/Content/Definitions/Items`
- `Assets/_Game/Content/Definitions/Affixes`
- `Assets/_Game/Content/Definitions/Rewards`
- `Assets/_Game/Content/Definitions/Expeditions`

## Test Execution

### EditMode
Run Unity Test Runner EditMode tests and confirm at minimum:

- stat calculation tests pass
- tactic priority tests pass
- synergy threshold tests pass
- reward pick tests pass
- persistence fallback tests pass once added to scene/runtime wiring

### PlayMode
Run at least one smoke test covering:

- project enters PlayMode
- a boot scene object exists or scene bootstrap path loads

## Manual MVP Flow Target

Current intended loop target:

- Boot -> Town -> Expedition -> Battle -> Reward -> Town

If the scene loop is not yet fully wired, note exactly where the run stops.

## Placeholder vs Real

### Real Implementations
- pure C# stat evaluation
- minimum combat loop
- minimum tactic evaluation
- minimum synergy application
- reward pick application
- Unity content definition asset types
- sample seed generator
- content validator
- JSON save repository

### Placeholder / Partial Areas
- scene-to-scene playable slice wiring
- Postgres CRUD adapter
- rich equipment flow in Unity scenes
- polished battle presentation
- advanced persistence integration into gameplay UI

## Exit Criteria for This Prototype Check

- seed generation succeeds
- content validation succeeds
- EditMode tests pass
- PlayMode smoke test passes
- JSON save fallback is not blocked by DB absence
- one playable run can be attempted end to end, or the exact broken transition is documented

## Open Questions

- which exact scene/controller should own first-load bootstrap?
- when should build scene registration be automated?
- what minimum debug HUD is needed before repeated playtests begin?
- when should manual playtest notes be promoted into structured telemetry?
