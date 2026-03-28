# Formation and Targeting

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines the MVP formation and targeting language for combat.

## MVP Rules

### Formation Structure

The battle formation is fixed to a 2-row layout with 4 deployed units total.
A simple MVP interpretation is:

- front row: 2 slots
- back row: 2 slots

This structure should remain stable during MVP so that positioning meaning is easy to read.

### Formation Intent

The formation should support:

- front-line protection roles
- back-line ranged or support roles
- readable target relationships
- simple positioning consequences

### Targeting Baseline

MVP targeting should remain intentionally narrow.
Target selection should derive from:

- tactic rule result
- valid range
- row accessibility or simple reach assumptions
- lowest HP logic when specified

### Recommended Baseline Target Rules

- front-row units generally target front-row enemies first if valid
- back-row targeting should depend on range, skill permission, or explicit tactic logic
- lowest HP enemy targeting should only work if the enemy is valid and targetable
- ally targeting should use the same validity logic rather than bypassing all constraints

### Implementation Preference

Use a simple and verifiable targeting validator.
Do not begin MVP with complex line-of-sight, collision, or spatial simulation.

## Long-Term Expansion Points

- taunt or guard mechanics
- formation shifts
- positional area skills
- targeting exceptions by class or skill
- movement or reordering during battle
- more detailed reach logic

Those can come later if the 2-row baseline proves readable first.

## Open Questions

- should MVP allow back-row units to be directly attacked only after front-row collapse?
- how much class-based targeting exception is needed for the first proof?
- should defend or guard alter target priority in MVP?
- does the battle need row-breaking skills in the first slice, or should that wait?
