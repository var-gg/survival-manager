# Technical Overview

- Status: draft
- Last Updated: 2026-03-29
- Owner: repository

## Purpose

This document defines the high-level technical architecture direction for `survival-manager`.
The goal is to keep the Unity project safe for incremental expansion by Codex while preserving clear ownership boundaries and human review points.

## Architectural Goal

Build a Unity project that is:

- data-driven where practical
- modular at the system boundary level
- safe to extend through repository-visible changes
- resistant to uncontrolled scene drift
- compatible with third-party asset intake without contaminating project-owned content

## Core Direction

The project should prefer:

- ScriptableObject-centered data catalogs
- prefab and settings asset composition
- explicit runtime system boundaries
- minimal direct scene editing
- repository-tracked documentation for content intake and structural decisions

The project should avoid:

- hidden scene-only wiring as the primary source of truth
- direct mutation of third-party vendor assets
- tightly coupled monolithic gameplay scripts spanning unrelated systems
- large risky changes without a sandbox validation path

## System Boundaries

The following system areas should remain explicitly separated in naming, data ownership, and code structure:

- combat
- units and enemies
- abilities
- items
- drops
- crafting
- progression
- save/load

These boundaries may communicate through controlled runtime contracts and shared data references, but should not collapse into one generic gameplay bucket.

## Preferred Authoring Model

Codex should prefer authoring through:

1. data assets
2. prefabs
3. settings/config assets
4. small targeted scripts
5. scene changes only when necessary

This keeps work diffable, reviewable, and safer to validate in automation.

## Third-Party Boundary

`Assets/ThirdParty` is treated as a vendor boundary.
Original imported assets should remain intact there.
Project-owned customization should live under `Assets/_Game` through wrappers, adapters, extension components, configuration assets, derived prefabs, and documentation.

## Sandbox-First Intake Principle

When introducing risky content or structure changes, the preferred flow is:

1. validate in sandbox or isolated experimental area
2. verify integration assumptions
3. review risks and ownership boundaries
4. promote only the approved subset into mainline project structure

## Human Review Required for High-Risk Changes

The following changes should be treated as high-risk and require explicit human review before or during merge:

- broad scene rewiring
- save format changes
- destructive asset moves or renames across major folders
- modifications that affect third-party import integrity
- rendering pipeline or project-wide settings changes
- automation that writes many Unity assets at once
- content migrations with unclear rollback

## Open Questions

- Which runtime systems should be pure code-driven versus asset-authored first?
- How much dependency injection or service registration structure is needed early?
- What is the smallest scene footprint that still supports safe iteration?
- Which architecture rule should be enforced by tooling first?
