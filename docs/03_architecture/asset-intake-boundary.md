# Asset Intake Boundary

- Status: draft
- Last Updated: 2026-03-29
- Owner: repository

## Purpose

This document defines the asset-intake boundary for `survival-manager`.
The goal is to make third-party asset adoption safe, reviewable, and compatible with Codex-assisted project growth.

## Core Boundary Rule

`Assets/ThirdParty` contains original vendor assets and should be treated as a protected boundary.
Project-owned modifications, wrappers, adaptations, and gameplay-facing integrations should live under `Assets/_Game`.

## Do Not Do

- do not directly edit original vendor files in `Assets/ThirdParty`
- do not move vendor originals into project-owned folders just to simplify references
- do not merge project gameplay logic into vendor scripts unless explicitly approved
- do not treat imported example scenes as production content by default

## Do Instead

- create wrapper prefabs under `Assets/_Game`
- create adapter or bridge scripts under `Assets/_Game/Scripts`
- create project-owned ScriptableObject catalogs that reference or parameterize imported content
- validate imported assets in sandbox before production promotion
- document integration assumptions and risks

## Sandbox-to-Mainline Flow

1. import or receive the third-party asset under `Assets/ThirdParty`
2. inspect structure, dependencies, and update risk
3. validate in sandbox scene, prefab, or integration harness
4. create project-owned wrappers and configuration assets under `Assets/_Game`
5. promote only the approved project-owned integration layer into the main gameplay path

## Human Review Required

The following cases require human review before approval:

- license or usage-right uncertainty
- vendor assets that appear to require direct modification for basic use
- plugin imports that affect global project settings
- rendering pipeline compatibility changes
- package imports with broad folder churn or generated code
- changes that would make future vendor updates hard to merge

## Acceptance Questions

Before an asset is adopted, answer:

- what problem is this asset solving?
- can the same value be achieved with lower integration risk?
- where does the project-owned wrapper layer live?
- what breaks when the vendor asset updates?
- what part of the intake requires manual review or visual QA?

## Open Questions

- Which asset categories are safe enough for lightweight intake?
- Which imported asset types most often force hidden project-wide changes?
- What sandbox checklist should become mandatory for every intake?
- How should wrapper naming and folder conventions be enforced?
