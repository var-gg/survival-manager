# ADR-0005: Persistence Boundary

- Status: accepted
- Last Updated: 2026-03-29
- Phase: prototype
- Decision Date: 2026-03-29

## Context

The project needs saveable runtime state for roster, expedition, rewards, and long-term progress.
If runtime state is mixed directly into content assets or designed around direct production DB assumptions, MVP development becomes fragile and overly coupled.

## Decision

Adopt a separate persistence boundary:

- gameplay definitions stay as Unity assets
- runtime and save state use separate persistence models
- DB is treated as an optional external adapter, not a core assumption
- direct production DB access is not assumed for MVP

## Consequences

### Positive

- save state remains portable and local-first
- runtime progress can evolve without mutating source content assets
- future persistence adapters can be added with less domain disruption

### Negative

- requires stable id discipline between definitions and saves
- save migration concerns appear earlier
- duplicated shape may exist between definition data and runtime state

## Follow-Up

- define save models around instance and progress state
- keep persistence behind application/infrastructure boundaries
- avoid leaking DB-specific concerns into domain logic
