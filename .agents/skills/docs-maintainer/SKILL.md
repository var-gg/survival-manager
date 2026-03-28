---
name: docs-maintainer
description: Create, update, organize, and sanity-check repository documentation, including links, indexes, and governance/architecture/setup docs.
---

# docs-maintainer

## Purpose

Use this skill for documentation maintenance work in the repository.
The emphasis is on keeping docs current, connected, and structurally coherent with actual implementation and policy.

## Applies To

- creating new repository documents
- updating existing docs after implementation changes
- fixing broken or outdated internal links
- refreshing section indexes or doc maps
- aligning governance, setup, and architecture docs
- tightening wording when structure or policy has changed

## Does Not Apply To

- implementing gameplay systems
- writing complex automation scripts
- changing Unity project configuration by itself without a documentation objective
- modifying vendor code under `Assets/ThirdParty`
- making product decisions that require a new approved direction

## Guardrails

- Keep documentation changes aligned with actual repository state.
- Prefer repository-relative links and stable document structure.
- If structure or policy changes, update related docs in the same work unit when practical.
- Do not directly modify original vendor contents under `Assets/ThirdParty`.

## Expected Outputs

- new or updated markdown docs
- cleaned links or references
- clarified ownership, scope, and source-of-truth notes
- lightweight documentation structure improvements
