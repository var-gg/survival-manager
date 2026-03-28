# Persistence Schema

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines what persistence data exists for MVP and what is explicitly excluded.

## Persisted Data

The MVP persistence boundary stores:

- profile
- hero instances
- inventory
- currencies
- unlocked permanent augments
- run summary

## Excluded Data

The MVP persistence boundary does not store:

- per-frame combat simulation state
- scene-local transient presentation state
- authored content definitions

## Definition Boundary

Content definitions are Unity assets.
Persistence references definitions by stable ids but does not own those definitions.

## Proposed Save Model

### Profile
- `profile_id`
- `display_name`

### Hero Instance
- `hero_id`
- `name`
- `archetype_id`
- `race_id`
- `class_id`
- `positive_trait_id`
- `negative_trait_id`
- `equipped_item_ids[]`

### Inventory Item
- `item_instance_id`
- `item_base_id`
- `affix_ids[]`
- `equipped_hero_id`

### Currency
- `gold`
- `trait_reroll_currency`

### Permanent Progress
- `unlocked_permanent_augment_ids[]`

### Run Summary
- `run_id`
- `expedition_id`
- `result`
- `gold_earned`
- `nodes_cleared`
- `completed_at_utc`

## Postgres Schema

Postgres local-dev schema name:

- `survival_manager`

## Adapter Policy

- JSON is the guaranteed fallback path
- Postgres is a local-dev adapter only at this phase
- runtime must not fail closed when DB is unavailable

## Open Questions

- should permanent augments be stored as a join table in all adapters or as a flat list in JSON only?
- how many run summaries should the MVP retain before pruning matters?
- what migration/version header should be added to the JSON format?
- when should profile-level metadata like seed history be persisted?
