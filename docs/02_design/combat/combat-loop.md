# Combat Loop

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines the combat loop language for the MVP.
The goal is to make combat implementation simple, readable, and verifiable before adding presentation complexity.

## MVP Rules

### Combat Shape

- combat is auto-battle
- battle formation uses 2 rows
- total deployed units per battle: 4
- internal combat resolution should use a simplified fixed-tick or round-based model
- presentation can look continuous later, but simulation should stay simple first

### Baseline Combat Sequence

1. battle starts with 4 deployed units in a 2-row formation
2. each unit evaluates its tactics list in priority order
3. the first satisfied rule selects an action and target
4. the action resolves under the current tick or round rules
5. unit state updates
6. next unit or next tick proceeds
7. combat ends when win or loss conditions are met

### Action Baseline

The starting action set should remain small:

- basic attack
- active skill
- wait / defend

### Why This Loop Exists

The player should feel that setup determines battle flow.
The implementation should make that relationship easy to test and explain.

## Long-Term Expansion Points

- more action categories
- richer timing rules
- interrupts or reactions
- more expressive presentation layers
- clearer battle replay or explanation tools

Long-term additions should not replace the simple underlying combat grammar unless the MVP loop is already proven.

## Open Questions

- should MVP use fixed-tick or round-based resolution as the first implementation?
- what is the minimum battle duration that still feels readable and meaningful?
- how should defend differ from wait in MVP, if at all?
- what is the smallest win/loss rule set that proves the loop cleanly?
