# ADR-0003: Content Pipeline Boundary

- Status: accepted
- Date: 2026-03-29

## Context

The project needs a content pipeline that remains safe for automation and review.
Direct scene-first workflows create hidden state, increase merge risk, and reduce Codex safety when the repository expands.

## Decision

Adopt a content pipeline boundary where:

- ScriptableObject-centered data catalogs are the preferred content-authoring model
- project growth should prefer data assets, prefabs, and settings assets over direct scene edits
- risky content changes should be validated in sandbox before production promotion
- scene edits should be narrow and justified rather than the default authoring path

## Consequences

### Positive

- safer and more diffable content changes
- stronger support for Codex-assisted expansion
- easier sandbox validation before promotion
- lower scene drift and hidden wiring risk

### Negative

- more discipline is required up front
- some one-off content tasks may take longer initially
- contributors may need extra docs and validation habits

## Follow-Up

- document recommended asset workflow
- add catalog validators over time
- identify high-risk scene changes that always need human review
