# ADR-0004: Third-Party Asset Policy

- Status: accepted
- Date: 2026-03-29

## Context

The project expects to evaluate or adopt third-party Unity assets.
Without a clear boundary, vendor originals may be mutated, ownership may blur, and future upgrades may become expensive or unsafe.

## Decision

Adopt a third-party asset policy where:

- original vendor assets remain under `Assets/ThirdParty`
- project-owned modifications, wrappers, and integration assets live under `Assets/_Game`
- Codex should not directly modify vendor originals during normal work
- third-party assets should be sandbox-validated before mainline integration
- high-risk imports and boundary-breaking changes require human review

## Consequences

### Positive

- vendor upgrades remain more manageable
- project ownership stays clearer
- risky integrations become easier to isolate
- accidental corruption of imported source assets becomes less likely

### Negative

- wrappers and adapters add some overhead
- certain assets may be rejected if they demand invasive modification
- some imports may require extra sandbox time before use

## Follow-Up

- keep asset intake boundary docs current
- standardize wrapper naming and folder conventions later
- document exceptions explicitly if policy must ever be broken
