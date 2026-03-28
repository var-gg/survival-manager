# Task Execution Pattern

- Status: active
- Owner: repository
- Last Updated: 2026-03-29
- Source of Truth: `docs/00_governance/task-execution-pattern.md`
- Applies To: Codex only

## Purpose

This document defines the execution-document pattern for long-running or multi-step work in the `survival-manager` repository.
Because important work in this project will often span multiple sessions, every major task should leave durable execution documents behind.

## Required Template Set

The repository provides the following task templates under `tasks/_templates/`:

- `spec.md`
- `plan.md`
- `implement.md`
- `status.md`

These templates are used to create task-scoped execution documents.

## Template Roles

### `spec.md`

Use `spec.md` to define what the task is.
It should capture:

- goal
- non-goals
- constraints
- deliverables
- done criteria

This file is used to prevent ambiguity and scope drift before implementation expands.

### `plan.md`

Use `plan.md` to define how the task will move forward.
It should capture:

- milestones
- approval criteria
- verification commands
- stop conditions

This file is used to structure execution and define when work must pause for review.

### `implement.md`

Use `implement.md` to define how the work should be carried out.
It should capture:

- working method
- scope limits
- documentation update rules
- test rules

This file keeps implementation discipline consistent across long-running work.

### `status.md`

Use `status.md` to record the live state of execution.
It should capture:

- current status
- completed
- on hold
- issues
- decisions
- next steps

This file is the durable progress surface for work that continues across sessions.

## Minimum Required Set

Every major task should create the minimum necessary subset of these four documents before execution starts.

Recommended default:

- use `spec.md` when task boundaries or acceptance need clarification
- use `plan.md` when the task has multiple milestones, approvals, or pause points
- use `implement.md` when execution discipline needs explicit rules
- use `status.md` whenever progress must survive across sessions

For simple work, not all four are required.
For major or long-running work, Codex should create the smallest set that still makes the task durable and reviewable.

## When This Pattern Is Mandatory

Codex should strongly prefer this pattern for:

- multi-session tasks
- milestone-based work
- tasks requiring explicit approval points
- tasks with meaningful risk of scope drift
- structure or policy changes that need durable tracking
- implementation efforts that need repeated status handoff

## Discord Reporting Rule

Discord completion and progress reports should be written as a compact summary of `status.md`.
That means reports should naturally mirror these categories:

- current status / completed
- on hold when relevant
- issues or risks
- decisions
- next steps

This keeps chat reporting aligned with durable task records instead of inventing a separate reporting style.

## Recommended Task Folder Pattern

A task folder may be created like this when needed:

```text
tasks/
  2026-03-feature-name/
    spec.md
    plan.md
    implement.md
    status.md
```

The exact task folder naming convention may evolve later, but each task should keep its execution docs together.

## Operating Notes

- Keep execution documents concise and operational.
- Update them when the task meaningfully changes.
- Do not create unnecessary template copies for trivial work.
- Prefer durable written decisions over relying on chat memory.
- Keep Discord reporting consistent with `status.md` summaries.
