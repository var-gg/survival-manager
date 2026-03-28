# Data Model

- Status: draft
- Last Updated: 2026-03-29
- Owner: repository

## Purpose

This document defines the preferred data-model direction for `survival-manager`.
The project should favor cataloged, inspectable, Unity-native assets over hardcoded scattered constants or scene-only configuration.

## Primary Strategy

Use a ScriptableObject-centered data catalog strategy as the default content-authoring model.

This means:

- major gameplay definitions should be representable as ScriptableObject assets
- runtime systems should consume stable data references rather than depend on scene wiring alone
- content growth should mostly add or compose assets rather than rewrite core logic

## Data Domain Boundaries

The following domains should have distinct data ownership:

- combat definitions
- unit definitions
- enemy definitions
- ability definitions
- item definitions
- drop tables
- crafting recipes
- progression tracks
- save data contracts

## Recommended Catalog Pattern

Each major domain should trend toward:

- a definition asset type
- optional grouped catalog/index assets
- runtime lookup services or bootstrap registries
- stable IDs for save/load and references where needed

Example conceptual shape:

- `UnitDefinition`
- `EnemyDefinition`
- `AbilityDefinition`
- `ItemDefinition`
- `DropTableDefinition`
- `CraftRecipeDefinition`
- `ProgressionTrackDefinition`

## Why ScriptableObject Catalogs

This direction helps Codex and humans because it:

- keeps content diffable in repository history
- reduces dependence on fragile scene state
- encourages explicit ownership by domain
- supports prefab/data composition
- makes sandbox validation easier before promotion

## Save Boundary Rule

Runtime save payloads should not serialize arbitrary live object graphs.
They should serialize stable identifiers and runtime state snapshots derived from known data catalogs.

Implications:

- save data should reference content by IDs, not by fragile scene paths
- catalog identity stability matters
- rename/move operations affecting saved references need review

## Preferred Folder Relationship

Data should generally be project-owned under `Assets/_Game`, not inside `Assets/ThirdParty`.
Third-party packages may provide source assets or templates, but project gameplay data should live in project-owned catalogs and wrappers.

## Human Review Required

The following data-model changes should receive explicit human review:

- save identifier schema changes
- catalog renames that may break references
- bulk migration of ScriptableObject asset types
- cross-domain merges that blur ownership boundaries
- generated asset pipelines that create or rewrite large numbers of content assets

## Open Questions

- Which domains need global catalogs versus local scoped references?
- What stable ID pattern should be adopted early for save-safe content?
- Which data assets should be hand-authored versus generated?
- How much validation tooling is needed before catalog count grows?
