# Unity MCP / Editor Bridge Policy Guide

- Status: draft
- Last Updated: 2026-03-29
- Owner: repository

## Purpose

This document defines the operating policy for a future Unity MCP or similar editor bridge.
At this stage, the project is documenting governance only.
No actual bridge package installation, external service connection, or editor automation dependency is approved by this document alone.

## Why This Exists

A Unity editor bridge may eventually help Codex or other tools inspect scenes, query assets, read logs, and make targeted project changes.
However, direct editor automation increases the risk of hidden state changes, scene churn, and accidental damage to project boundaries.

The goal is to define safe operating rules before any integration is attempted.

## Allowed Task Categories

If a bridge is adopted later, the following categories are potentially allowed under normal operating policy:

### Read-Oriented Operations

- scene hierarchy inspection
- game object/component inspection
- asset search
- prefab reference lookup
- log and console inspection
- project setting reads
- serialized field inspection where safely supported

### Narrow Write-Oriented Operations

- create or modify game objects in sandbox or explicitly approved scenes
- create or modify project-owned prefabs under `Assets/_Game`
- create or update project-owned ScriptableObject assets
- assign references in project-owned wrappers or test assets
- update clearly scoped configuration values in project-owned assets

### Validation-Oriented Operations

- run targeted scene queries
- inspect broken references
- inspect import errors or missing components
- collect structured reports for review

## Blocked or Deferred Task Categories

The following categories are prohibited or deferred unless explicitly approved for a specific task:

- bulk deletion of assets, game objects, or folders
- direct modification of `Assets/ThirdParty`
- package import without explicit human approval
- project-wide find/replace style editor rewrites
- uncontrolled scene-wide rewiring
- destructive rename/move operations across major folders
- save-system affecting migrations without explicit review
- project settings changes with broad rendering/input/build impact

## Boundary Rules

Any future bridge use must still respect repository architecture rules:

- `Assets/ThirdParty` remains protected
- project-owned integration belongs under `Assets/_Game`
- prefer data/prefab/settings edits over broad scene mutation
- validate risky changes in sandbox before promotion
- do not use editor convenience as a reason to bypass documented architecture boundaries

## Failure and Recovery Strategy

If bridge-driven changes misbehave or create uncertainty, use the following recovery options:

1. stop further editor-driven writes
2. inspect the exact changed files in git
3. revert specific files with git checkout or equivalent restore flow
4. isolate retries on a separate branch
5. move risky experimentation into sandbox scenes or sandbox project areas
6. if needed, reproduce in an isolated sandbox project before retrying main project integration

Bridge usage should never assume editor state is self-healing.
Recovery must remain repository-visible and reviewable.

## Human Verification Required

A human should directly verify the results when a bridge is used for:

- scene object creation or structural edits in non-sandbox scenes
- prefab changes that affect widely reused gameplay assets
- changes related to rendering, cameras, lighting, or UI readability
- asset reference rewiring with gameplay impact
- any change that may affect save/load assumptions
- any operation that touches package, plugin, or global project settings

## Candidate Evaluation Criteria

When later comparing official or community editor bridges, evaluate them against the following criteria:

- can it operate with narrow permissions?
- does it clearly separate read-only from write operations?
- are changes visible in normal repository diffs?
- does it avoid hidden project-wide state mutation?
- can it target sandbox workflows safely?
- does it preserve Unity version compatibility and upgrade clarity?
- does it have clear maintenance status and community trust?
- can it be disabled cleanly if it causes issues?

## Adoption Guardrails

Before any bridge is installed or enabled, require:

- a concrete use case
- a review of package/source trust
- a defined rollback path
- a sandbox-first trial plan
- confirmation that existing file-based workflows are insufficient for the target task

## Non-Decision Note

This document does **not** approve installation of any specific Unity MCP, editor bridge, package, plugin, or service.
It only defines the policy framework for evaluating and operating one later.

## Open Questions

- Which read-only bridge capabilities would deliver immediate value with low risk?
- What is the minimum write scope worth allowing at all?
- Should bridge writes be limited to sandbox by default even after adoption?
- What audit logging would be necessary if bridge usage becomes common?
