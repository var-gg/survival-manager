---
name: asset-intake
description: Review and organize third-party asset intake with emphasis on evaluation, wrapping strategy, integration notes, and safe handling boundaries.
---

# asset-intake

## Purpose

Use this skill when evaluating or preparing the intake of third-party assets, plugins, or vendor-delivered content.
The goal is to make intake disciplined without contaminating project-owned areas or mutating vendor originals.

## Applies To

- reviewing whether a third-party asset should be imported
- documenting intake checklists and acceptance criteria
- defining wrapping or adapter strategy around vendor assets
- deciding where integration notes and ownership boundaries should live
- identifying project-owned extension points outside vendor folders
- documenting risks, version assumptions, and dependency boundaries

## Does Not Apply To

- directly editing original asset or plugin source under `Assets/ThirdParty`
- making gameplay decisions unrelated to asset intake
- broad repository refactors unrelated to the imported asset
- release publishing workflow work
- full automation for package ingestion

## Guardrails

- Treat `Assets/ThirdParty` as a vendor boundary.
- Do not directly modify original vendor contents under `Assets/ThirdParty`.
- Prefer wrappers, adapters, settings, and project-owned integration layers.
- Record integration assumptions and risks in docs.
- Keep intake work narrowly scoped to the asset being evaluated.

## Expected Outputs

- intake review notes
- integration/wrapping guidance
- checklist documents
- ownership boundary clarification for third-party content
