# Technical Overview

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document fixes the MVP technical architecture direction.
The goal is to support a small playable slice without hardcoding every content rule into scene scripts or giant switches.

## MVP Rules

### Architectural Direction

The project should separate:

- content definitions
- runtime instances
- persistence state
- presentation adapters

The intent is to keep the domain model small, testable, and expandable.

### Key Boundary Rules

- definitions and instances are separate concepts
- content definitions are Unity asset centered
- runtime state lives in a separate persistence-oriented model
- combat simulation should live in a pure C# area with minimal UnityEngine coupling
- DB, UI, and Scene are adapters outside core domain logic
- direct production DB access is not assumed

### Core Extensibility Points

The MVP architecture should reserve first-class extension seams for:

- Stat
- Modifier
- Trigger
- Condition
- Effect
- Reward

These should be modeled as data-driven concepts, not scattered one-off special cases.

## Proposed Folder Layout

```text
Assets/_Game/
  Scripts/
    Runtime/
      Domain/
        Core/
        Combat/
        Meta/
        Progression/
      Application/
        UseCases/
        Services/
      Infrastructure/
        Persistence/
        Authoring/
        Random/
      Presentation/
        UI/
        Scene/
        BattleView/
    Editor/
      Authoring/
      Validation/
  Content/
    Definitions/
      Stats/
      Traits/
      Classes/
      Races/
      Skills/
      Items/
      Affixes/
      Augments/
      Rewards/
      Encounters/
  Tests/
    EditMode/
    Runtime/
```

## Proposed asmdef Split

Recommended assembly split:

- `SurvivalManager.Domain`
- `SurvivalManager.Application`
- `SurvivalManager.Infrastructure`
- `SurvivalManager.Presentation`
- `SurvivalManager.Editor`
- `SurvivalManager.Tests`

### Dependency Direction

- Domain depends on no Unity presentation systems
- Application depends on Domain
- Infrastructure depends on Domain and Application contracts
- Presentation depends on Application and Domain read models
- Editor depends on Domain, Application contracts, and authoring assets
- Tests may depend on all needed runtime assemblies

## Proposed Namespace Direction

- `SurvivalManager.Domain.*`
- `SurvivalManager.Application.*`
- `SurvivalManager.Infrastructure.*`
- `SurvivalManager.Presentation.*`
- `SurvivalManager.Editor.*`

## Long-Term Expansion Points

- richer authoring validation
- more formal service boundaries
- save migration versioning
- alternative persistence adapters
- telemetry and replay adapters

## Open Questions

- should MVP begin with a separate Application layer immediately, or introduce it once use cases multiply?
- how much of reward generation belongs in Domain versus Infrastructure randomness helpers?
- which authoring validations are mandatory before content count grows?
- what is the smallest assembly split that still prevents accidental Unity coupling into combat simulation?
