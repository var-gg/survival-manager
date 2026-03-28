# Testing Strategy

- Status: draft
- Last Updated: 2026-03-29
- Owner: repository

## Purpose

This document defines the current testing strategy direction for `survival-manager`.
The goal is to support safe, incremental project growth with a bias toward low-cost validation first.

## Testing Priorities

The project should prefer this validation order:

1. document and structure checks
2. data/catalog validation
3. EditMode smoke tests
4. focused integration tests
5. PlayMode tests where runtime behavior justifies them
6. broader manual play validation

## Why This Order

The project is early and architecture-sensitive.
Low-cost checks should catch drift before expensive runtime validation is needed.

## Coverage Areas

Testing should gradually cover:

- folder and structure invariants
- presence and integrity of key assets
- ScriptableObject catalog validity
- domain boundary assumptions
- prefab integration assumptions
- save/load contract stability

## Scene Testing Bias

Do not make scenes the only validation surface.
Prefer validations that can run against data, prefabs, and settings assets first.
Scene-driven checks are allowed when behavior cannot be validated another way.

## Sandbox Validation

New risky content should be validated in sandbox before mainline promotion.
Sandbox validation may include:

- isolated prefabs
- temporary test scenes
- asset catalog probes
- smoke tests against imported content wrappers

## Human Review Required

Automated tests do not replace human review for:

- visual readability
- gameplay feel
- third-party licensing or source integrity concerns
- broad scene or settings migrations
- changes that may alter save compatibility

## High-Risk Testing Triggers

The following changes should trigger extra validation and human attention:

- changes to save/load contracts
- major content catalog additions or migrations
- rendering or pipeline setting changes
- replacement of core prefabs used widely across content
- changes touching third-party import boundaries

## Open Questions

- What catalog validators should be built first?
- Which domain deserves the first deeper EditMode integration tests?
- When does PlayMode become necessary instead of optional?
- What should the minimum manual regression checklist contain?
