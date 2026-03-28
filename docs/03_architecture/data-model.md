# Data Model

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document fixes the MVP data-model direction so that authored content and runtime state do not become mixed together.

## MVP Rules

### Definition vs Instance

The project should explicitly separate:

- Definition: authored content blueprint
- Instance: runtime or save-backed realization of that blueprint

Examples:

- `HeroArchetypeDefinition` versus `HeroInstance`
- `SkillDefinition` versus `SkillRuntimeState`
- `ItemDefinition` versus `ItemInstance`
- `TraitDefinition` versus `TraitRollInstance`

### Definition Storage Rule

Content definitions should be Unity assets first.
Recommended asset roots:

```text
Assets/_Game/Content/Definitions/
```

### Runtime State Rule

Runtime state should not be stored back into authored Unity definition assets.
Runtime and save state should be represented by separate persistence-friendly models.

### Stat Model Direction

Do not prematurely freeze the stat space into a giant enum unless truly necessary.
Prefer evaluation of:

- `StatDefinition`
- stable string or numeric ids through a registry

Recommended concept:

- `StatDefinition` asset defines semantic meaning
- stable id registry maps authoring ids to runtime-safe references

### Extensibility Model

The following concepts should be treated as reusable data-driven nodes:

- Stat
- Modifier
- Trigger
- Condition
- Effect
- Reward

### Polymorphic Authoring Rule

For `Condition` and `Effect`, prefer a data-node structure over one giant switch statement.
Possible authoring pattern:

- abstract authored node base
- concrete node assets or serialized references
- runtime factory or translator layer

## Example Model Sketch

```text
Definitions
  StatDefinition
  ClassDefinition
  RaceDefinition
  SkillDefinition
  TraitDefinition
  ItemDefinition
  AffixDefinition
  AugmentDefinition
  RewardDefinition

Instances
  HeroInstance
  SquadInstance
  ItemInstance
  ExpeditionInstance
  CombatUnitState
  RewardOfferState
```

## Proposed Namespaces

- `SurvivalManager.Domain.Definitions`
- `SurvivalManager.Domain.Model`
- `SurvivalManager.Domain.Combat`
- `SurvivalManager.Infrastructure.Authoring`
- `SurvivalManager.Infrastructure.Persistence.Models`

## Long-Term Expansion Points

- schema validation tooling
- id migration helpers
- richer graph-style authored effects
- formal runtime compilation of authored nodes

## Open Questions

- should stable ids be strings, ints, GUID-backed strings, or generated hashes?
- should MVP authoring use ScriptableObject per definition type or grouped catalogs for some content?
- how early should runtime compilation cache authored nodes for performance and validation?
- where should cross-definition validation live: Editor only or shared validation services?
