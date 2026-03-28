# Asset Workflow

- Status: draft
- Last Updated: 2026-03-29
- Owner: repository

## Purpose

This document explains the recommended working workflow for adding, validating, and promoting assets in `survival-manager`.
It is intended to keep Codex and human contributors aligned on safe asset handling.

## Working Rule Summary

- keep vendor originals in `Assets/ThirdParty`
- keep project-owned wrappers and gameplay assets in `Assets/_Game`
- prefer data/prefab/settings changes over direct scene edits
- validate in sandbox before mainline use
- escalate high-risk changes for human review

## Recommended Workflow

### 1. Intake

- place imported third-party source assets in `Assets/ThirdParty`
- record what the asset is for and what system it may affect
- identify whether it touches rendering, input, save, or other broad project concerns

### 2. Sandbox Validation

- create isolated validation assets or scenes
- test the minimum integration path
- confirm whether the asset can be used without editing the vendor original
- identify required wrappers, adapters, and project-owned config assets

### 3. Project-Owned Integration

- create wrappers in `Assets/_Game`
- create project ScriptableObject data assets or catalogs as needed
- create prefabs or config assets that isolate the imported dependency
- avoid making production scenes the first integration site

### 4. Promotion

- promote only reviewed and sandboxed assets into the main gameplay path
- document assumptions, naming, and maintenance risks
- ensure references point to project-owned assets where possible

## High-Risk Changes Requiring Human Review

- direct edits to vendor originals
- large scene rewiring
- project-wide settings changes
- rendering pipeline changes
- save/load relevant identity changes
- imports with unclear license or update behavior

## Local Validation Checklist

- smoke check passes
- docs or intake notes updated
- wrappers exist in project-owned folders
- no accidental vendor-original edits
- sandbox validation result is understandable to a reviewer

## Open Questions

- What sandbox folder convention should be standardized next?
- Should intake review use a checklist template under `tasks/`?
- Which imported asset categories deserve separate workflow documents?
- What should the minimum visual QA checklist include?
