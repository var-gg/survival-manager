# ADR-0002: Prototype Phase Promotion

- Status: accepted
- Last Updated: 2026-03-29
- Phase: prototype
- Decision Date: 2026-03-29

## Context

The repository previously operated under skeleton-stage constraints.
The project now needs a limited promotion that allows implementation work toward a playable proof without dissolving the safety boundaries established earlier.

## Decision

Promote the repository state from `skeleton` to `prototype`.

Under this promotion:

- the current phase objective becomes a **playable vertical slice at wooden-dummy fidelity**
- implementation is limited to `Assets/_Game/**` and `Assets/Tests/**`
- direct modification of original vendor contents under `Assets/ThirdParty/**` remains prohibited
- documentation and implementation updates should continue to land together whenever practical

## Consequences

### Positive

- allows narrow implementation work to begin
- preserves clear safe zones for Codex-driven changes
- keeps third-party boundaries intact
- gives the repository a concrete short-horizon delivery target

### Negative

- still constrains broad refactors and project-wide code generation
- may require extra discipline when tempting shortcuts exist outside approved zones
- prototype progress can be slowed if boundaries are ignored and must be corrected later

## Follow-Up

- track the vertical-slice work under `tasks/001_mvp_vertical_slice/`
- keep governance docs aligned with the prototype boundary
- defer broader repository-wide implementation until explicitly approved

## Open Questions

- what specific proof points must the wooden-dummy slice demonstrate?
- what signals justify promotion beyond prototype later?
- which architecture guardrails are most likely to be pressure-tested first?
