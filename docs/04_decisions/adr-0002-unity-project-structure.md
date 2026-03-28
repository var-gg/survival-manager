# ADR-0002: Unity Project Structure

- Status: accepted
- Date: 2026-03-29

## Context

The project needs a Unity structure that can grow safely through Codex-assisted changes.
A scene-heavy or vendor-mixed structure would increase risk, reduce reviewability, and make ownership boundaries unclear.

## Decision

Adopt a project structure where:

- `Assets/_Game` contains project-owned gameplay content, wrappers, definitions, prefabs, and integration assets
- `Assets/ThirdParty` contains vendor-original imported assets
- data, prefabs, and settings assets are preferred over direct scene editing for normal extension work
- major gameplay domains remain structurally separable

## Consequences

### Positive

- clearer ownership boundaries
- safer automation surface
- easier review of Codex-authored changes
- lower risk of contaminating third-party originals

### Negative

- more upfront wrapper/setup work
- some integrations may feel slower initially
- contributors must follow the structure intentionally

## Follow-Up

- reinforce the structure in architecture docs
- keep third-party intake policy explicit
- expand folder conventions as concrete systems land
