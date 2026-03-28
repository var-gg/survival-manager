# ADR-0004: Combat Simulation Boundary

- Status: accepted
- Last Updated: 2026-03-29
- Phase: prototype
- Decision Date: 2026-03-29

## Context

Combat needs to remain readable, testable, and less coupled to Unity-specific scene behavior.
If combat rules are authored directly into MonoBehaviours and scene orchestration, the MVP will become hard to verify and hard to extend safely.

## Decision

Place combat simulation in a pure C# oriented domain area with minimal UnityEngine coupling.
Unity scenes, animation, and battle visuals should act as adapters around combat state and outcomes rather than as the source of battle truth.

## Consequences

### Positive

- combat logic becomes easier to unit test
- simulation can evolve without rewriting scene glue repeatedly
- presentation timing can be layered later without redefining combat truth

### Negative

- requires translation between domain simulation and presentation state
- may feel slower initially than direct scene scripting for quick hacks
- adapter boundaries must be maintained deliberately

## Follow-Up

- keep `SurvivalManager.Domain.Combat` free of presentation assumptions where possible
- let presentation consume outcomes, events, or read models
- avoid inventing battle truth inside scene scripts
