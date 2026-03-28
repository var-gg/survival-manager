# Design Pillars

- Status: draft
- Last Updated: 2026-03-29
- Phase: concept framing

## Purpose

This document defines the current design pillars for `survival-manager`.
These are not final feature promises.
They are directional constraints used to judge future design choices.

## Pillar 1. Readable Auto-Battle Strategy

The player should be able to understand why their setup succeeded or failed.
Even if combat is internally turn-based or rule-driven, the visible battle should feel continuous and legible.

Implications:

- combat readability matters more than raw spectacle
- cause-and-effect should be explainable from formation, priorities, and build choices
- the player should be able to observe meaningful tactical outcomes without constant direct control

## Pillar 2. Planning and Configuration Are the Core Skill

The game should reward shaping a plan before and between fights.
Mastery should come primarily from composition, tactical setup, deck choices, loot evaluation, and progression planning.

Implications:

- direct moment-to-moment input is not the primary mastery axis
- setup decisions should feel consequential
- tactical rules must be powerful enough to matter, but simple enough to read

## Pillar 3. Managed Complexity, Not System Pile-Up

The game can support layered progression, but the player should not be buried under too many simultaneous systems.
Depth should come from interaction quality, not from stacking every possible subsystem.

Implications:

- systems should unlock or become relevant gradually
- each run should emphasize a few meaningful decisions
- deck, loot, crafting, and tactics must reinforce the same fantasy

## Pillar 4. Satisfying Observation and Adjustment Loop

Watching the battle unfold should itself be rewarding.
The game should deliver the pleasure of seeing a prepared plan execute, then learning how to adjust the next setup.

Implications:

- battle readability matters for spectatorship and self-analysis
- failure should produce actionable insight
- player motivation should come from refinement, not only reaction speed

## Pillar 5. Steam-Readable Identity

The game must communicate its fantasy quickly: tactical setup, auto-battle execution, and repeatable build growth.
A hybrid concept only helps if players can grasp the hook within seconds.

Implications:

- trailer readability matters
- store messaging should not depend on explaining hidden system layers in detail
- the pitch should fit a small set of memorable differentiators

## Rejection Criteria

A future feature should be questioned if it:

- increases combat noise without improving readable tactical outcomes
- shifts mastery away from planning/configuration toward twitch control
- adds a new progression layer without reducing ambiguity elsewhere
- makes the game harder to pitch on Steam
- depends on direct imitation of a reference title

## Open Questions

- How much battle intervention should be allowed once a fight begins?
- Which is more important to sell: auto-battle readability or deep setup flexibility?
- Which progression layer is most essential for retention: deck, loot, crafting, or meta unlocks?
- What does the store-facing one-sentence pitch become if only two pillars survive?
