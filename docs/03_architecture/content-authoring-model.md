# Content Authoring Model

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines how gameplay content should be authored for MVP.
The goal is to let a small amount of content drive the slice without hardcoding behavior into scene logic.

## MVP Rules

### Authoring Source of Truth

Gameplay content definitions should be authored primarily as Unity assets under project-owned folders.
Recommended root:

```text
Assets/_Game/Content/Definitions/
```

### Node-Based Authoring Direction

Polymorphic content such as `Condition` and `Effect` should be authored as node-like data structures rather than giant switch-based blobs.
Recommended pattern:

- authoring-time node objects or referenced sub-assets
- translation into runtime-ready domain nodes
- validation before entering play mode or build pipelines where practical

### Recommended Authoring Areas

```text
Assets/_Game/Content/Definitions/
  Stats/
  Traits/
  Classes/
  Races/
  Skills/
  Conditions/
  Effects/
  Items/
  Affixes/
  Augments/
  Rewards/
  Encounters/
```

### Authoring Validation Rule

The authoring model should support validation for:

- stable id uniqueness
- missing references
- invalid condition/effect node links
- illegal reward tables
- invalid stat references

## Proposed Editor Support

Suggested editor-only areas:

```text
Assets/_Game/Scripts/Editor/Authoring/
Assets/_Game/Scripts/Editor/Validation/
```

Suggested namespaces:

- `SurvivalManager.Editor.Authoring`
- `SurvivalManager.Editor.Validation`

## Long-Term Expansion Points

- custom editors for tactics authoring
- graph-like editors for effect composition
- batch validation reports
- content linting for reward and balance ranges

## Open Questions

- should `Condition` and `Effect` nodes be sub-assets, serialized references, or dedicated asset types in MVP?
- what minimum validation set must block broken content from being committed?
- how much custom editor tooling is justified before the playable slice exists?
- which content types should remain hand-authored first versus generated from templates?
