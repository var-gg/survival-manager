# Content Seed Assets

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Asset Types Added

CreateAssetMenu-enabled content definition types:

- StatDefinition
- RaceDefinition
- ClassDefinition
- TraitPoolDefinition
- UnitArchetypeDefinition
- SkillDefinitionAsset
- AugmentDefinition
- ItemBaseDefinition
- AffixDefinition
- ExpeditionDefinition
- RewardTableDefinition

## CreateAssetMenu Paths

All new definition assets are exposed under:

- `SM/Definitions/...`

## Seed Generator

A sample seed generator is provided via:

- `SM/Seed/Generate Sample Content`

This generator creates the initial prototype asset set under:

- `Assets/_Game/Content/Definitions/`

## Validator

A validator is provided via:

- `SM/Validation/Validate Content Definitions`

Current checks include:

- duplicate IDs across definitions
- missing archetype references
- missing reward table references on expedition nodes
- missing tactic presets
- trait pools not meeting the documented 3 positive / 3 negative baseline

## Recommended Seed Counts

The generator is designed around the current MVP recommendation:

- races: 3
- classes: 4
- archetypes: 8
- temporary augments: 9
- permanent augments: 3
- item bases: 6
- affixes: 8

## Battle Demo Asset Usage

The following assets are intended to be used first by the battle demo:

### Stats
- `stat_max_health`
- `stat_attack`
- `stat_defense`
- `stat_speed`
- `stat_heal_power`

### Races
- `human`
- `beastkin`
- `undead`

### Classes
- `vanguard`
- `duelist`
- `ranger`
- `mystic`

### Skills
- `skill_power_strike`
- `skill_precision_shot`
- `skill_minor_heal`

### Archetypes
- `warden`
- `guardian`
- `slayer`
- `raider`
- `hunter`
- `scout`
- `priest`
- `hexer`

### Reward / Expedition
- `rewardtable_battle`
- `rewardtable_expedition_end`
- `expedition_mvp_demo`

## Notes

- trait pools are documented as archetype-level 3 positive / 3 negative pools
- data reuse is allowed through repeated trait patterns across different archetype pools
- primitive visuals are acceptable at this phase; data variety is the priority

## Open Questions

- should trait definitions become standalone assets later instead of embedded pool entries?
- when should augment and item effects move from simple stat modifiers toward richer effect nodes?
- what subset of these assets should be loaded for the first Boot -> Battle demo path?
- when do reward tables need weighting rather than flat lists?
