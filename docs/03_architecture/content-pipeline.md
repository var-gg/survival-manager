# Content Pipeline

- Status: draft
- Last Updated: 2026-03-29
- Owner: repository

## Purpose

This document defines the preferred content pipeline direction for `survival-manager`.
The goal is to keep content intake and project growth safe, reviewable, and compatible with Codex-driven expansion.

## Pipeline Principle

Prefer data assets, prefabs, and settings assets over direct scene editing whenever practical.
Scene changes should be the last step, not the primary authoring surface.

## Preferred Pipeline Order

1. Define or update data contracts
2. Create or update ScriptableObject content assets
3. Create or update project-owned prefabs and wrappers
4. Validate in sandbox or isolated test area
5. Promote approved assets/configuration into production folders
6. Touch scenes only if the content cannot be activated another way

## Project-Owned Content Rule

Project-owned gameplay content should live under `Assets/_Game`.
This includes:

- gameplay definitions
- derived prefabs
- integration wrappers
- balancing assets
- project settings assets specific to game behavior

## Third-Party Intake Rule

Third-party originals stay under `Assets/ThirdParty`.
Codex should not directly mutate vendor originals as part of normal intake or extension work.
Instead, the project should create:

- wrapper prefabs
- adapter scripts
- bridge configuration assets
- project-owned documentation
- sandbox verification assets

## Sandbox Validation Rule

Before promoting new content into the main gameplay path, validate it in a sandbox or isolated integration path.
Examples include:

- experimental test scenes
- sandbox prefabs
- temporary catalog entries marked for review
- isolated test harnesses

Sandbox results should answer:

- does the asset integrate cleanly?
- does it violate ownership boundaries?
- does it create hidden dependencies?
- does it create merge or upgrade risk?

## High-Risk Change Categories

The following content changes should be reviewed explicitly by a human:

- batch prefab rewrites
- scene-wide object relinking
- asset migration across ownership boundaries
- replacement of core project settings assets
- import flows that generate large opaque metadata churn
- changes that may affect save compatibility or runtime identity

## Automation Preference

If Codex automates content work, prefer automation that writes:

- data assets
- project-owned prefabs
- settings/config assets
- validation reports

Avoid automation that primarily edits live scenes unless the change is narrow, reviewable, and necessary.

## Open Questions

- Which kinds of sandbox content should be kept versus cleaned up after validation?
- What promotion checklist is needed before sandbox assets become production assets?
- Which content checks should become automated first?
- How should asset IDs and naming conventions be validated over time?
