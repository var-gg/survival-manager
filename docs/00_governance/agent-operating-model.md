# Codex Agent Operating Model

- Status: active
- Owner: repository
- Last Updated: 2026-03-29
- Source of Truth: `docs/00_governance/agent-operating-model.md`
- Applies To: Codex only

## Purpose

This document defines the detailed operating model for Codex working inside the `survival-manager` repository.
It exists to keep execution disciplined, documentation synchronized, and reporting consistent.

## Relationship to `AGENTS.md`

- `AGENTS.md` contains short, top-level, non-negotiable rules.
- This document explains how those rules are applied during actual work.
- If there is any ambiguity, follow `AGENTS.md` first.

## Core Operating Rules

### 1. Keep docs and implementation together

Codex should update documentation and implementation in the same work unit whenever practical.
This applies especially to:

- new systems
- folder structure changes
- scene/bootstrap changes
- workflow or policy changes
- package or dependency changes

If implementation changes but documentation is deferred, Codex should explicitly call out the gap as a risk.

### 2. Update structure and policy docs with the change

When a task changes repository structure, development policy, workflow, or technical direction, Codex must update the related documentation and ADR first or in the same change set.

Typical targets include:

- governance docs under `docs/00_governance/`
- architecture docs under `docs/03_architecture/`
- setup docs under `docs/05_setup/`
- ADR documents when a design or policy decision becomes durable

Codex should avoid leaving structural decisions undocumented.

### 3. Do not modify original vendor files in `Assets/ThirdParty`

`Assets/ThirdParty` is treated as an imported/vendor area.
Codex must not directly edit original third-party source files there unless the task explicitly authorizes a vendor patch workflow.

Preferred approaches:

- configure through project settings
- wrap third-party code from project-owned code
- document version and integration assumptions
- isolate local extensions outside original vendor roots

If a third-party patch becomes unavoidable, Codex should stop and ask for explicit approval.

### 4. Prevent unnecessary scope expansion

Codex should finish the requested task cleanly without opportunistically widening scope.
Examples of disallowed behavior:

- adding unrelated refactors
- rewriting naming conventions without approval
- changing architecture beyond the task boundary
- introducing new systems because they seem useful
- bundling extra cleanup that changes review surface significantly

Allowed behavior:

- minimal adjacent fixes required to complete the requested task safely
- documentation updates required by the change
- reporting a follow-up recommendation without doing it automatically

### 5. Report in Korean

All human-facing progress reports and completion summaries should be written in Korean.
The following should remain in English where applicable:

- code
- commands
- file names
- folder names
- identifiers
- package names
- branch names
- commit messages when a repository convention requires English

## Working Style

### Plan size

Prefer small, reviewable task units.
When possible, make changes that are easy to validate in one pass.

### File discipline

Prefer editing the smallest set of files needed to complete the task.
Do not create new governance or process files unless the task asks for them.

### Consistency

When adding a new rule, structure, or convention, ensure that related docs do not contradict it.
If contradictions exist, resolve them in the same task when practical.

### Risk visibility

When there is uncertainty, report it explicitly under risk rather than masking it with confident language.

## Documentation Update Triggers

Codex should consider documentation updates mandatory when a task changes any of the following:

- project structure
- package/dependency policy
- scene/bootstrap flow
- coding workflow policy
- branching or handoff policy
- reporting format
- durable technical decisions

## ADR Expectation

Create or update an ADR when a task makes a durable choice about:

- architecture boundaries
- dependency policy
- repository structure
- third-party integration policy
- project workflow rules

If an ADR is not created during such a change, Codex should explain why.

## Completion Standard

A task is considered cleanly complete when all of the following are true where relevant:

- requested change is implemented
- directly affected documentation is updated
- avoidable scope expansion did not occur
- third-party vendor boundaries were respected
- the report is delivered in Korean
- remaining risks or follow-ups are explicitly stated
