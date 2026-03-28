# Task Status: 001 MVP Vertical Slice

- Status: active
- Last Updated: 2026-03-29
- Phase: prototype
- Task ID: 001

## Current State

Prototype-phase transition documented.
Playable-slice task track created.
A pure C# minimum rules engine now exists for Core, Combat, and Meta domains.
Unity content definition types, seed generation, validation, and persistence boundaries are documented and partially implemented.
The repository is ready for Unity-side playable slice wiring, but the full scene loop is not yet verified end to end.

## Completed in This Step

- repository state promoted from `skeleton` to `prototype`
- implementation boundary clarified as `Assets/_Game/**` and `Assets/Tests/**`
- third-party direct-modification prohibition reaffirmed
- documentation/implementation sync rule reaffirmed
- vertical-slice task shell created
- `SM.Core` minimum primitives implemented: ids, tags, stat keys, modifier ops, stat evaluation, RNG, result type
- `SM.Combat` minimum loop implemented: unit snapshots, team battle state, tactic evaluation, target selection, damage/heal resolution, synergy application
- `SM.Meta` minimum loop implemented: roster state, expedition state, recruit candidate shape, reward pick application, currency state
- Unity content definition asset types added for stats, races, classes, traits, archetypes, skills, augments, items, affixes, expeditions, and reward tables
- sample seed generator and content validator added through editor menus
- persistence abstractions, JSON save fallback, Postgres local-dev boundary, and SQL schema/bootstrap scripts added
- operating docs added for local runbook, playtest checklist, current known issues, persistence setup, and balance knobs
- EditMode tests added for stat calculation, tactic priority, synergy threshold activation, reward selection reflection, battle resolution, and JSON persistence roundtrip
- PlayMode smoke test added for baseline runtime entry

## Next State

Next work should focus on actual Unity scene/controller implementation for the playable loop:

- Boot -> Town -> Expedition -> Battle -> Reward -> Town
- primitive visual presentation and debug UI
- gameplay persistence integration into the live scene flow

## Risks

- full scene loop is still not implemented/verified despite the underlying domain and content layers existing
- Postgres remains a placeholder local-dev adapter rather than a full implementation
- generated content assets depend on Unity editor execution and validation
- current PlayMode coverage is intentionally shallow
- ADR numbering collisions still exist in earlier documents and should be normalized later
