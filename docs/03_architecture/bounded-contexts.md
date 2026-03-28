# Bounded Contexts

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines the MVP bounded-context proposal so that game rules do not collapse into one giant code bucket.

## MVP Rules

### Proposed Contexts

#### 1. Content Definition Context

Responsible for authored data assets such as:

- stat definitions
- race/class definitions
- skill definitions
- trait definitions
- item and affix definitions
- reward definitions

Suggested namespace:

- `SurvivalManager.Domain.Definitions`
- `SurvivalManager.Infrastructure.Authoring`

#### 2. Combat Simulation Context

Responsible for:

- formation
- targeting
- tactics rule evaluation
- stat resolution
- action execution
- combat outcome

Suggested namespace:

- `SurvivalManager.Domain.Combat`

This context should remain minimally coupled to UnityEngine.

#### 3. Meta Loop Context

Responsible for:

- town loop
- recruitment
- expedition progression
- reward allocation
- augment acquisition
- roster decisions

Suggested namespace:

- `SurvivalManager.Domain.Meta`
- `SurvivalManager.Domain.Progression`

#### 4. Persistence Context

Responsible for:

- save models
- runtime state snapshots
- serialization format boundaries
- versioning and migration hooks

Suggested namespace:

- `SurvivalManager.Infrastructure.Persistence`
- `SurvivalManager.Application.Save`

#### 5. Presentation Context

Responsible for:

- scene orchestration
- UI rendering
- battle visuals
- player input surfaces

Suggested namespace:

- `SurvivalManager.Presentation.UI`
- `SurvivalManager.Presentation.Scene`
- `SurvivalManager.Presentation.BattleView`

## Adapter Rule

DB, UI, and Scene are adapters around the domain model.
They may translate into and out of domain/application flows, but they should not become the location where core rules are invented.

## Long-Term Expansion Points

- external analytics adapters
- cloud save adapters
- replay adapters
- automated balancing utilities

## Open Questions

- should recruitment and expedition flow remain in one meta context for MVP, or split earlier?
- how much reward-generation logic should live beside expedition logic versus standalone reward services?
- which read models need to be separate from write models even in MVP?
- what is the earliest signal that a context boundary is leaking?
