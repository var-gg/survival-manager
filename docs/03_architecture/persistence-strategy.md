# Persistence Strategy

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines the MVP persistence boundary.
The goal is to preserve runtime state cleanly without coupling the game domain to production DB assumptions.

## MVP Rules

### Persistence Boundary

Content definitions are Unity assets.
Runtime and save state are separate persistence models.
These models should be serializable without requiring direct references back into live authored objects.

### Production DB Rule

Do not assume direct production DB access.
The MVP should work with local persistence-first approaches.
DB integration, if ever needed later, should be treated as an external adapter.

### Adapter Rule

DB, UI, and Scene are adapters outside the domain logic.
Persistence implementation should sit behind interfaces or service boundaries rather than leaking into combat or meta rules.

### Suggested Persistence Areas

```text
Assets/_Game/Scripts/Runtime/Application/Save/
Assets/_Game/Scripts/Runtime/Infrastructure/Persistence/
```

Suggested namespaces:

- `SurvivalManager.Application.Save`
- `SurvivalManager.Infrastructure.Persistence`
- `SurvivalManager.Infrastructure.Persistence.Models`

### MVP Save Scope

Recommended MVP save scope includes:

- roster state
- hero instances
- item instances
- expedition progress snapshot
- currencies
- permanent augment state
- trait reroll currency state

### MVP Persistence Style

A simple local serialized save model is sufficient for MVP.
Examples include JSON-backed or other straightforward local formats.
The format matters less than keeping domain state portable and versionable.

## Long-Term Expansion Points

- migration/version pipelines
- cloud save adapters
- server-backed persistence adapters
- conflict handling and profile management

## Open Questions

- should MVP start with JSON snapshots or a more structured save format immediately?
- how should definition ids be stored inside saves to remain stable across content iteration?
- what minimum migration/version header is needed from day one?
- what state is safe to recompute at load time versus requiring persistence?
