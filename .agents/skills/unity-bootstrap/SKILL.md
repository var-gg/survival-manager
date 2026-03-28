---
name: unity-bootstrap
description: Prepare or refine the initial Unity project skeleton, baseline settings, folder layout, and lightweight test scaffolding.
---

# unity-bootstrap

## Purpose

Use this skill for early-stage Unity project setup and stabilization.
This skill is about establishing a clean base structure, not building full gameplay systems.

## Applies To

- creating or refining initial `Assets` folder structure
- setting baseline Unity project organization
- documenting bootstrap assumptions
- preparing lightweight test scaffolding
- checking setup-related project files and repository alignment
- organizing project-owned folders outside vendor roots

## Does Not Apply To

- implementing full gameplay features
- writing production gameplay architecture in depth
- importing and patching third-party assets directly
- modifying original vendor contents under `Assets/ThirdParty`
- building complex CI/CD or release automation

## Guardrails

- Keep bootstrap work minimal, reviewable, and reversible where possible.
- Prefer project-owned folders such as `_Game`, `Tests`, and governance/docs areas.
- Keep setup docs and implementation changes together when practical.
- Do not directly modify original vendor contents under `Assets/ThirdParty`.
- Do not widen scope into unrelated gameplay or tooling work.

## Expected Outputs

- initial Unity folder layout changes
- setup documentation updates
- baseline test or scene scaffolding notes
- project structure cleanup for early development
