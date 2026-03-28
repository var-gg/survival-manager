# ADR-0003: Data-Driven Content Boundary

- Status: accepted
- Last Updated: 2026-03-29
- Phase: prototype
- Decision Date: 2026-03-29

## Context

The project needs enough flexibility to support tactics, traits, augments, items, rewards, and combat rules without hardcoding every content case into scene logic or giant switch statements.

## Decision

Adopt a data-driven content direction with the following rules:

- separate Definition from Instance
- author gameplay content primarily as Unity assets
- treat `Stat`, `Modifier`, `Trigger`, `Condition`, `Effect`, and `Reward` as first-class extensibility seams
- prefer `StatDefinition` plus stable id registry over prematurely freezing all stats into a giant enum
- author polymorphic `Condition` and `Effect` structures as node-based data rather than one giant switch

## Consequences

### Positive

- smaller content additions should require less code churn
- rules become easier to validate and test
- content iteration can happen without forcing scene-script growth

### Negative

- authoring and validation complexity rises earlier
- some runtime translation layers are needed
- stable id discipline becomes mandatory

## Follow-Up

- keep authoring assets under project-owned content folders
- add validation support before content count grows too much
- keep giant switch logic as a last resort, not default design
