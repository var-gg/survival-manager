# Balance Knobs

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document lists the current tuning surfaces that can change MVP feel without re-architecting systems.

## Core Combat Knobs

- base max health per archetype
- base attack per archetype
- base defense per archetype
- base speed per archetype
- base heal power per archetype
- basic attack damage formula floor
- active skill power values
- heal skill power values
- front/back row range assumptions

## Tactics Knobs

- tactic rule priority ordering
- ally HP threshold for heal behavior
- target selector choice per archetype
- fallback defend behavior

## Synergy Knobs

- race synergy threshold counts
- race synergy attack bonus values
- class synergy threshold counts
- class synergy defense bonus values
- future synergy family thresholds and modifier payloads

## Trait / Item / Augment Knobs

- trait modifier values
- negative trait penalties
- item base modifier values
- affix modifier values
- temporary augment rarity and modifier values
- permanent augment unlock strength

## Meta / Reward Knobs

- starting roster composition
- recruit candidate generation pool size
- reroll frequency and reroll currency income
- reward table composition
- gold reward values
- permanent augment drop timing
- node count and expedition pacing

## Persistence / Progression Knobs

- how many run summaries to keep
- what currencies persist between runs
- which augments are temporary versus permanent
- what return-to-town summary data is surfaced

## Next-Step Backlog

- PVP tuning surfaces
- advanced crafting input/output tuning
- more synergy families and thresholds
- external asset integration constraints and readability tuning

## Open Questions

- which knobs should move into dedicated designer-facing assets first?
- what balancing values must remain code-level until the playable slice stabilizes?
- when does the current single-skill-per-archetype approach become too limiting?
- what telemetry is needed before changing reward pacing aggressively?
