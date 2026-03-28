# Combat Premise

- Status: draft
- Last Updated: 2026-03-29
- Phase: concept framing

## Purpose

This document defines the current combat-premise hypothesis for `survival-manager`.
It is intended to frame what combat should feel like before detailed mechanics, stats, or encounter tables are designed.

## Premise Hypothesis

Combat should feel like a readable auto-battle unfolding from player preparation.
Internally, combat may resolve through turn-based or tick-based rules, but the player-facing experience should feel continuous unless the player deliberately slows, pauses, or inspects details.

The game is not trying to deliver classic explicit-turn tactics.
It is trying to deliver the satisfaction of watching a well-prepared tactical setup succeed under pressure.

## Desired Combat Feel

- readable threat escalation
- visible consequences from formation and role setup
- low-input or no-input battle flow during active resolution
- enough pace to sustain run energy
- enough clarity to support post-battle analysis and next-step adjustment

## Current Directional Assumptions

- combat may run continuously from the player's perspective
- player agency may come primarily from formation, behavior priorities, deck choices, loot, and progression decisions
- live intervention, if present, should be limited and high-value rather than constant
- the internal rule model should make outcomes consistent enough to learn from

## Constraints

- combat readability must survive unit density, effects, and UI pressure
- internal turn/tick logic must create understandable outcomes rather than hidden randomness-like confusion
- tactical depth cannot depend on excessive manual control during fights
- combat rules should be explainable to a Steam audience quickly

## Reference Notes

Reference inspirations are used only to identify feel targets such as readable automation, tactical intent, unit-role expression, and satisfying observation.
They are not implementation blueprints.

## Combat Risks

- combat may look passive if observation and management payoff are not strong enough
- hidden rule resolution may feel opaque if player feedback is weak
- battle outcomes may feel predetermined if reconfiguration opportunities are too sparse
- visual intensity may exceed the player's ability to diagnose why something happened

## Open Questions

- Is combat fundamentally squad-based, leader-plus-support, or another hybrid structure?
- How much live intervention should be allowed once a fight begins?
- How should the game surface internal rule resolution without making combat feel turn-paused?
- What is the minimum combat prototype that proves readable auto-battle management is fun?
