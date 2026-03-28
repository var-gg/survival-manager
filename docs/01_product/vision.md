# Vision

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document fixes the current product vision for `survival-manager`.
The immediate goal is to lock the MVP direction before implementation scope spreads.

## Product Definition

`survival-manager` is a management-driven strategy game built from three reference directions:

- Unicorn Overlord-style tactical setup and behavior planning
- Teamfight Tactics-style race/class synergy pressure
- Darkest Dungeon-style expedition and return loop

This is not a direct-control action game and not a classic stop-and-command SRPG.
Combat should resolve as auto-battle, while player mastery comes from setup, composition, roster decisions, and return-loop planning.

## MVP Vision

The MVP should prove that the player can:

- assemble a small roster with meaningful race/class identity
- choose a squad from a larger town-held roster
- deploy a limited front-line battle formation
- watch a readable 3D auto-battle resolve from prior decisions
- return from expedition with gains, losses, and next-step pressure

The MVP must prefer simple and verifiable internal combat logic over ambitious systemic complexity.

## Long-Term Vision

In the longer term, the project may expand toward richer expedition structure, broader roster expression, deeper content variety, and more robust progression layers.
However, long-term ambition must not weaken MVP clarity.
The first proof is a wooden-dummy playable vertical slice, not a content-complete strategy RPG.

## Fixed MVP Direction

- combat presentation: 3D
- combat control: auto-battle
- combat simulation priority: simple, readable, verifiable internal rules
- player skill focus: tactical setup, synergy selection, roster management, expedition planning

## MVP Scope Anchors

The current MVP fixed values are:

- battle deployment size: 4
- expedition roster size: 8
- town-held roster cap: 12
- race count: 3
- class count: 4
- recruit archetype count: 8
- temporary augments: 9 total
  - silver: 3
  - gold: 3
  - platinum: 3
- permanent augment slots in MVP: 1
- equipment slots: 3
  - weapon
  - armor
  - accessory

## Recommended MVP Roster Base

Recommended MVP playable identity set:

- races: Human, Beastkin, Undead
- classes: Vanguard, Duelist, Ranger, Mystic

## What Must Be Proven First

The MVP is successful only if it proves:

- auto-battle observation is satisfying enough without direct control
- roster and synergy choices create understandable outcomes
- the expedition and return loop creates forward pressure
- the narrow roster cap still generates meaningful decisions

## 열린 질문

- what is the minimum expedition structure needed to make return decisions meaningful?
- how much tactical rule customization is enough for MVP without reducing readability?
- what battle feedback is necessary so players understand why an auto-battle result happened?
- which long-term progression ideas should remain explicitly deferred until after the MVP proof?
