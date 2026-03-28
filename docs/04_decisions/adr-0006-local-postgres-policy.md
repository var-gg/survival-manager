# ADR-0006: Local Postgres Policy

- Status: accepted
- Last Updated: 2026-03-29
- Phase: prototype
- Decision Date: 2026-03-29

## Context

The project needs persistence for MVP progress, but persistence must not block local gameplay iteration.
The architecture already treats content definitions as Unity assets and avoids direct production DB assumptions.

## Decision

Adopt the following persistence policy:

- persist profile, hero instances, inventory, currencies, unlocked permanent augments, and run summary
- do not persist per-frame combat state to DB
- use Postgres schema name `survival_manager`
- read connection information only from environment variables or local config
- keep JSON fallback available so the game remains playable without Postgres
- treat Postgres as a local development adapter, not a launch-time production assumption
- keep authored content definitions in Unity assets rather than DB tables

## Consequences

### Positive

- gameplay does not depend on DB availability
- local iteration remains fast
- DB concerns stay outside authored content ownership
- future adapter work has a documented boundary

### Negative

- two persistence paths must be understood
- Postgres may lag behind JSON until local-dev DB work becomes necessary
- save model discipline remains important across adapters

## Follow-Up

- keep JSON as the safe default path
- document schema/bootstrap SQL for local Postgres
- avoid pushing domain logic into DB-specific code
- do not assume client-to-production DB connectivity in MVP planning
