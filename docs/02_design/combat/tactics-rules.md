# Tactics Rules

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines the MVP tactics rule language.
The goal is to keep tactical automation understandable and authorable before adding complexity.

## MVP Rules

### Core Rule Format

Each tactics entry should be evaluated as:

`condition -> action -> target`

Rules are checked in priority order from top to bottom.
The first valid rule is selected.
If no earlier rule is valid, the unit should fall back to the default rule.

### MVP Action Types

The MVP starting action types are:

- basic attack
- active skill
- wait / defend

### MVP Condition Types

The MVP starting condition types are:

- self HP
- ally HP
- enemy in range
- lowest HP enemy
- default fallback

### Example Rule Shapes

- if self HP below threshold -> wait / defend -> self
- if ally HP below threshold -> active skill -> lowest HP ally
- if enemy in range -> basic attack -> nearest valid enemy
- if lowest HP enemy is targetable -> active skill -> lowest HP enemy
- fallback -> basic attack or wait / defend -> default valid target

### Evaluation Goals

The tactics system should:

- be readable by humans
- be serializable in simple data structures
- be easy to test in isolation
- produce understandable combat outcomes

## Long-Term Expansion Points

- more conditions
- more target filters
- more action categories
- rule grouping or weighting
- class-specific tactic templates
- player-authored presets and shared loadouts

Long-term tactics growth should only happen if the base rule chain remains legible.

## Open Questions

- how many tactics slots should one hero have in MVP?
- should fallback always be mandatory?
- should active skill selection be explicit per rule or handled by a smaller priority helper?
- what is the simplest editor UX for authoring rule order without overbuilding tools too early?
