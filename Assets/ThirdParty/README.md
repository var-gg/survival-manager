# ThirdParty

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This folder contains external or vendor-origin assets.

## Current Rules

- do not directly modify original vendor contents under `Assets/ThirdParty/**`
- project-owned wrappers, derived prefabs, and integrations should live under `Assets/_Game/**`
- this boundary remains in force during prototype

## Open Questions

- which imported packs need explicit provenance notes?
- when do we need a manifest of vendor asset sources and versions?
- what validation is needed to prevent accidental edits inside this area?
- which third-party content, if any, will be required for the first playable slice?
