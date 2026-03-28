# Town and Expedition Loop

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document fixes the MVP meta-loop structure for `survival-manager`.
The goal is to ensure the game is not just a battle simulator, but a loop of preparation, risk, reward, and return.

## MVP Rules

### Core Loop

The MVP loop is:

1. town upkeep
2. hero recruitment and equipment organization
3. expedition departure
4. battle and reward resolution
5. return to town

This order should remain stable during MVP.

### Expedition Shape

The MVP expedition should begin as a small branching map of about 5 nodes.
This is enough to create route choice and reward tension without requiring a full adventure structure.

### Node Intent

The initial 5-node style expedition should support:

- at least one route choice
- at least one combat reward moment
- a sense of committing a squad to a run
- a meaningful return with changed resources or roster state

### Why This Loop Matters

The game promise is not only "set up a team and watch them fight."
It is also "prepare a squad, take a risk, come back changed, and decide what to do next."

## Long-Term Expansion Points

- longer expeditions
- more node types
- risk escalation systems
- town facilities
- event nodes
- PVP as a long-term vision only, not an MVP target

## Economic Runaway Risks

- if expeditions pay too much gold, roster and item pressure collapses
- if node rewards are too dense, route choice becomes obvious rather than strategic
- if return rewards stack too fast, the player exits the MVP tuning envelope too early

## Balance Risks

- a 5-node map may feel too short if rewards do not create meaningful change
- if expedition losses are too light, return decisions may feel hollow
- if expedition losses are too harsh, roster variety may collapse into only safest picks

## Data Expansion Points

- node definition catalogs
- expedition map templates
- route reward tables
- return-state event definitions

## Open Questions

- should the first MVP expedition be exactly 5 nodes or a 4-6 node envelope?
- what is the minimum loss/consequence needed to make return meaningful?
- how much route information should the player see before committing?
- what town actions matter most immediately after returning?
